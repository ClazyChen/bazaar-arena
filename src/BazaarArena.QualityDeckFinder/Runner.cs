using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena.QualityDeckFinder;

/// <summary>主循环：虚拟赛季（代表选择 → 匹配赛 → 卡组优化 → 注入/放弃）＋周期性 Top10 与状态保存。</summary>
public static class Runner
{
    private static int GetCumulativeGameCount(OptimizerState state, string comboSig)
    {
        int v = state.VirtualPlayerPool.TryGetValue(comboSig, out var ve) ? ve.GameCount : 0;
        int h = state.HistoryPool.TryGetValue(comboSig, out var he) ? he.GameCount : 0;
        return Math.Max(v, h);
    }

    public static void Run(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config)
    {
        var rng = state.RngSeed.HasValue ? new Random(state.RngSeed.Value) : new Random();
        PerfCounters.Enabled = config.Perf;

        if ((state.HistoryPool.Count == 0 && state.VirtualPlayerPool.Count == 0) || state.AnchoredPlayerComboSig.Count == 0)
        {
            SeedPoolWithVirtualPlayers(simulator, db, pool, state, config, rng);
            Console.WriteLine($"[初始化完成] 历史池 {state.HistoryPool.Count}，虚拟玩家池 {state.VirtualPlayerPool.Count}，开始虚拟赛季循环。");
        }

        if (config.Workers > 0)
        {
            RunWithWorkers(simulator, db, pool, state, config, rng);
            return;
        }

        int seasonsSinceTop = 0;
        int seasonsSinceSave = 0;

        while (true)
        {
            RunSeason(simulator, db, pool, state, config, rng);
            state.CurrentSeason++;
            seasonsSinceSave++;

            if (config.TopInterval == 1)
            {
                Top10Report.Print(state);
                seasonsSinceTop = 0;
            }
            else
            {
                seasonsSinceTop++;
                if (seasonsSinceTop >= config.TopInterval)
                {
                    seasonsSinceTop = 0;
                    Top10Report.Print(state);
                }
            }

            if (seasonsSinceSave >= config.SaveInterval)
            {
                seasonsSinceSave = 0;
                StatePersistence.Save(config.StatePath, state);
                Console.WriteLine($"[已保存状态到 {config.StatePath}]");
            }

            if (config.MaxSeasons > 0 && state.CurrentSeason >= config.MaxSeasons)
            {
                Console.WriteLine($"[已达最大赛季数 {config.MaxSeasons}，输出最终报告并保存后退出]");
                Top10Report.Print(state);
                StatePersistence.Save(config.StatePath, state);
                Console.WriteLine($"[已保存状态到 {config.StatePath}]");
                return;
            }
        }
    }

    /// <summary>多 worker 模式：赛季循环，匹配赛与优化阶段内对局可并行（当前仍为单线程执行赛季）。</summary>
    private static void RunWithWorkers(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config, Random mainRng)
    {
        int seasonsSinceTop = 0;
        int seasonsSinceSave = 0;
        while (true)
        {
            RunSeason(simulator, db, pool, state, config, mainRng);
            state.CurrentSeason++;
            seasonsSinceSave++;

            if (config.TopInterval == 1)
            {
                Top10Report.Print(state);
                seasonsSinceTop = 0;
            }
            else
            {
                seasonsSinceTop++;
                if (seasonsSinceTop >= config.TopInterval)
                {
                    seasonsSinceTop = 0;
                    Top10Report.Print(state);
                }
            }
            if (seasonsSinceSave >= config.SaveInterval)
            {
                seasonsSinceSave = 0;
                StatePersistence.Save(config.StatePath, state);
                Console.WriteLine($"[已保存状态到 {config.StatePath}]");
            }

            if (config.MaxSeasons > 0 && state.CurrentSeason >= config.MaxSeasons)
            {
                Console.WriteLine($"[已达最大赛季数 {config.MaxSeasons}，输出最终报告并保存后退出]");
                Top10Report.Print(state);
                StatePersistence.Save(config.StatePath, state);
                Console.WriteLine($"[已保存状态到 {config.StatePath}]");
                return;
            }
        }
    }

    /// <summary>执行一个虚拟赛季：代表选择 → 匹配赛 → 卡组优化 → 赛季结束（入池/注入/合并）。</summary>
    private static void RunSeason(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config, Random rng)
    {
        PerfCounters.SeasonBegin(state.TotalGames);

        int localOptRestarts = 0;
        int abandonRestarts = 0;

        var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var representatives = AnchoredRepresentativeScheduler.SelectRepresentatives(state, config, rng);
        PerfCounters.AddRepTicks(System.Diagnostics.Stopwatch.GetTimestamp() - t0);

        // 参赛计数：仅当锚定玩家本季被选为代表参赛时才计入，用于“长期不改进放弃”判定。
        foreach (var anchoredKey in representatives.Values)
            state.AnchoredParticipatedSeasonsSinceImproved.AddOrUpdate(anchoredKey, 1, (_, v) => v + 1);

        var activeComboSigs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in representatives.Values)
        {
            if (state.AnchoredPlayerComboSig.TryGetValue(key, out var sig))
                activeComboSigs.Add(sig);
        }

        if (activeComboSigs.Count == 0) return;

        Console.WriteLine($"[赛季 {state.CurrentSeason + 1}] 活跃玩家 {activeComboSigs.Count}，总对局 {state.TotalGames}，开始匹配赛...");

        // 本季参赛池快照：仅包含本季活跃玩家，用于匹配与打印口径（不把非活跃虚拟玩家混入）
        var seasonPool = new Dictionary<string, ComboEntry>(StringComparer.Ordinal);
        foreach (var sig in activeComboSigs)
        {
            if (state.VirtualPlayerPool.TryGetValue(sig, out var e))
                seasonPool[sig] = e;
        }

        // 调试口径：活跃玩家 ELO 分布（用于排查“赛季后活跃玩家全落最低段”）
        if (seasonPool.Count > 0)
        {
            double minElo = double.MaxValue, maxElo = double.MinValue, sumElo = 0;
            int initialCount = 0;
            foreach (var e in seasonPool.Values)
            {
                minElo = Math.Min(minElo, e.Elo);
                maxElo = Math.Max(maxElo, e.Elo);
                sumElo += e.Elo;
                if (Math.Abs(e.Elo - config.InitialElo) < 0.0001) initialCount++;
            }
            Console.WriteLine($"  [调试] 活跃ELO: min={minElo:F1}, max={maxElo:F1}, avg={sumElo / seasonPool.Count:F1}, ==Initial {initialCount}/{seasonPool.Count}");
        }

        // 匹配赛：受 SeasonMatchCap / SeasonLossCap 限制；多 worker 时每轮并行跑局、单线程合并写池
        if (config.Workers > 0)
            RunMatchPhaseParallel(seasonPool, state, config, simulator, db, rng);
        else
            RunMatchPhaseSingle(seasonPool, state, config, simulator, db, rng);

        Console.WriteLine("  匹配赛结束。");

        // 卡组优化：仅锚定代表参与优化
        Console.WriteLine($"  卡组优化：锚定代表 {representatives.Count}。");
        var anchoredStart = System.Diagnostics.Stopwatch.GetTimestamp();
        int hillRuns = 0;
        int hillStopNoNeighbors = 0;
        int hillStopNoImprove = 0;
        int hillStopMaxSteps = 0;
        int hillIterationsSum = 0;
        int hillNeighborsSampledSum = 0;
        int hillNeighborsEvaluatedSum = 0;
        int hillZeroNeighborIterSum = 0;
        double hillBestDeltaMax = double.NegativeInfinity;
        double hillBestDeltaSum = 0;
        int hillFoundImprovementCount = 0;
        int hillLocalOptButPositiveDeltaCount = 0;
        foreach (var itemName in representatives.Keys.ToList())
        {
            var key = representatives[itemName];
            if (!state.AnchoredPlayerComboSig.TryGetValue(key, out var comboSig)) continue;
            if (!state.VirtualPlayerPool.TryGetValue(comboSig, out var entry)) continue;
            var anchorItem = AnchoredRepresentativeScheduler.ItemNameFromKey(key);
            string newSig;
            DeckRep newRep;
            bool isLocalOptimum;
            HillClimb.RunDiag diag = default;
            if (config.HillClimbDiag)
            {
                (newSig, newRep, isLocalOptimum, diag) =
                    HillClimb.RunWithDiag(comboSig, entry.Representative, state, config, pool, simulator, db, rng, anchorItemName: anchorItem);
            }
            else
            {
                (newSig, newRep, isLocalOptimum) =
                    HillClimb.Run(comboSig, entry.Representative, state, config, pool, simulator, db, rng, anchorItemName: anchorItem);
            }
            if (config.HillClimbDiag)
            {
                hillRuns++;
                hillIterationsSum += diag.Iterations;
                hillNeighborsSampledSum += diag.TotalNeighborsSampled;
                hillNeighborsEvaluatedSum += diag.TotalNeighborsEvaluated;
                hillZeroNeighborIterSum += diag.ZeroNeighborIterations;
                if (!double.IsNegativeInfinity(diag.BestDeltaEloSeen))
                {
                    hillBestDeltaSum += diag.BestDeltaEloSeen;
                    hillBestDeltaMax = Math.Max(hillBestDeltaMax, diag.BestDeltaEloSeen);
                }
                switch (diag.StopReason)
                {
                    case HillClimb.StopReason.NoNeighbors: hillStopNoNeighbors++; break;
                    case HillClimb.StopReason.NoImprovementInSample: hillStopNoImprove++; break;
                    case HillClimb.StopReason.ReachedMaxSteps: hillStopMaxSteps++; break;
                }
                if (!isLocalOptimum) hillFoundImprovementCount++;
                if (isLocalOptimum && diag.StopReason == HillClimb.StopReason.NoImprovementInSample && diag.BestDeltaEloSeen > 0)
                    hillLocalOptButPositiveDeltaCount++;
                if (isLocalOptimum && diag.WhenLocalOpt_NextNotNull && diag.WhenLocalOpt_NextElo > diag.WhenLocalOpt_CurrentElo)
                    Console.WriteLine($"  [诊断] 判断层异常: next!=null 且 nextElo({diag.WhenLocalOpt_NextElo:F1})>currentElo({diag.WhenLocalOpt_CurrentElo:F1}) 却判局部最优");
            }
            if (string.IsNullOrEmpty(newSig)) continue;

            // 锚定玩家达到局部最优：停止继续优化，重启该锚定玩家从随机起点继续探索其他路线
            if (isLocalOptimum)
            {
                RestartAnchoredPlayer(simulator, db, pool, state, config, rng, key, itemName);
                localOptRestarts++;
                state.AnchoredParticipatedSeasonsSinceImproved[key] = 0;
                continue;
            }

            // 采纳新组合时，避免把 ELO 重置到 InitialElo（会导致下一赛季“活跃玩家”段位分布塌缩到最低段）。
            // 正常情况下 HillClimb 已对候选跑过对局并写回池，这里直接复用其 ELO；
            // 若极端情况下池里还没有该条目，则继承当前锚定玩家的 ELO，保持赛季间段位连续性。
            var hasNewEntry = state.TryGetEntry(newSig, out var ne);
            var newElo = hasNewEntry ? ne.Elo : entry.Elo;
            if (newSig != comboSig && (!hasNewEntry || newElo > entry.Elo))
            {
                state.AnchoredPlayerComboSig[key] = newSig;
                state.AnchoredLastImprovedSeason[key] = state.CurrentSeason;
                state.AnchoredParticipatedSeasonsSinceImproved[key] = 0;
                int baseGames = GetCumulativeGameCount(state, newSig);
                state.VirtualPlayerPool[newSig] = new ComboEntry(newSig, newRep, newElo, false, false, baseGames);
                EloSystem.TryAddToHistoryPool(state, config, newSig, newRep, newElo);
            }
        }
        PerfCounters.AddHillClimbAnchoredTicks(System.Diagnostics.Stopwatch.GetTimestamp() - anchoredStart);
        Console.WriteLine("    锚定代表优化完成。");
        if (config.HillClimbDiag && hillRuns > 0)
        {
            Console.WriteLine($"  [诊断] HillClimb: runs={hillRuns}, 找到改进={hillFoundImprovementCount}, stop(noNeighbors/noImprove/maxSteps)={hillStopNoNeighbors}/{hillStopNoImprove}/{hillStopMaxSteps}");
            Console.WriteLine($"  [诊断] HillClimb: iterAvg={(double)hillIterationsSum / hillRuns:F2}, neighborsSampledAvg={(double)hillNeighborsSampledSum / hillRuns:F1}, neighborsEvalAvg={(double)hillNeighborsEvaluatedSum / hillRuns:F1}, zeroNeighborIterAvg={(double)hillZeroNeighborIterSum / hillRuns:F2}");
            Console.WriteLine($"  [诊断] HillClimb: bestDeltaEloSeen avg={(hillBestDeltaSum / hillRuns):F1}, max={(double.IsNegativeInfinity(hillBestDeltaMax) ? 0 : hillBestDeltaMax):F1}");
            if (hillLocalOptButPositiveDeltaCount > 0)
                Console.WriteLine($"  [诊断] 局部最优但曾见更优邻居(bestDelta>0): {hillLocalOptButPositiveDeltaCount}/{hillRuns} ← 疑似判断层bug");
        }

        var abandonInjectStart = System.Diagnostics.Stopwatch.GetTimestamp();
        abandonRestarts = ApplyAbandon(simulator, db, pool, state, config, rng);
        PerfCounters.AddAbandonInjectTicks(System.Diagnostics.Stopwatch.GetTimestamp() - abandonInjectStart);
        if (abandonRestarts > 0)
            Console.WriteLine($"  [调试] 放弃重启锚定玩家 {abandonRestarts} 个（ELO 继承，不重置 Initial）");

        // 赛季末重建历史池：对「历史池 + 虚拟玩家池」并集按段位上限裁剪，避免 ELO 漂移后段位堆积失控
        EloSystem.RebuildHistoryPoolAtSeasonEnd(state, config);

        Console.WriteLine($"  [调试] 重启汇总：局部最优重启 {localOptRestarts}，长期不改进放弃重启 {abandonRestarts}");
        PerfCounters.PrintSeasonSummary(state.CurrentSeason + 1, state.TotalGames);
    }

    private static void PrintMatchPhasePoolInfo(OptimizerState state, Config config, IReadOnlyDictionary<string, ComboEntry> seasonPool)
    {
        int maxSeg;
        lock (config.SegmentBoundsLock)
        {
            maxSeg = config.SegmentBounds.Count;
        }
        var historySegCounts = new List<int>();
        var seasonSegCounts = new List<int>();
        var unionSegCounts = new List<int>();

        // 赛季参赛池与并集：以“可查到条目的签名”为准；并集去重
        var unionSigs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sig in state.HistoryPool.Keys)
            unionSigs.Add(sig);
        foreach (var sig in seasonPool.Keys)
            unionSigs.Add(sig);

        for (int s = 0; s <= maxSeg; s++)
        {
            historySegCounts.Add(state.SignaturesInSegment(state.HistoryPool, s).Count);

            int seasonCount = 0;
            foreach (var e in seasonPool.Values)
                if (state.SegmentIndex(e.Elo) == s) seasonCount++;
            seasonSegCounts.Add(seasonCount);

            int unionCount = 0;
            foreach (var sig in unionSigs)
            {
                if (!state.TryGetEntry(sig, out var e)) continue;
                if (state.SegmentIndex(e.Elo) == s) unionCount++;
            }
            unionSegCounts.Add(unionCount);
        }

        Console.WriteLine($"  历史池：池大小 {state.HistoryPool.Count}，各段人数 [{string.Join(", ", historySegCounts)}]");
        Console.WriteLine($"  本季参赛池：池大小 {seasonPool.Count}，各段人数 [{string.Join(", ", seasonSegCounts)}]");
        Console.WriteLine($"  可匹配池(并集)：池大小 {unionSigs.Count}，各段人数 [{string.Join(", ", unionSegCounts)}]");
    }

    /// <summary>单线程匹配赛：逐玩家、逐批对局并立即更新池。</summary>
    private static void RunMatchPhaseSingle(
        IReadOnlyDictionary<string, ComboEntry> seasonPool,
        OptimizerState state,
        Config config,
        SimulatorClass simulator,
        IItemTemplateResolver db,
        Random rng)
    {
        PrintMatchPhasePoolInfo(state, config, seasonPool);
        const int progressInterval = 50;
        int processed = 0;
        var seasonSigs = seasonPool.Keys.ToList();
        foreach (var comboSig in seasonSigs)
        {
            processed++;
            if (processed % progressInterval == 0)
                Console.WriteLine($"  匹配赛：已处理 {processed}/{seasonSigs.Count} 个活跃玩家，总对局 {state.TotalGames}");
            if (!seasonPool.TryGetValue(comboSig, out var entry)) continue;
            var rep = entry.Representative;
            double elo = entry.Elo;
            int games = 0;
            int losses = 0;
            while (games < config.SeasonMatchCap && losses < config.SeasonLossCap)
            {
                var opps = EloSystem.SelectOpponentSignaturesForSeason(state, config, seasonSigs, isNewDeck: false, elo, Math.Max(1, config.GamesPerEval), rng, excludeComboSig: comboSig, useSegmentNeighborhood: true);
                opps = opps.Take(Math.Max(1, config.GamesPerEval)).ToList();
                if (opps.Count == 0) break;
                var runStart = System.Diagnostics.Stopwatch.GetTimestamp();
                var (newElo, gamesPlayed, lossDelta) = RunGamesAndCountLosses(comboSig, rep, opps, state, config, simulator, db);
                PerfCounters.AddMatchRunTicks(System.Diagnostics.Stopwatch.GetTimestamp() - runStart);
                PerfCounters.AddMatchGames(gamesPlayed);
                elo = newElo;
                games += gamesPlayed;
                losses += lossDelta;
                if (state.VirtualPlayerPool.TryGetValue(comboSig, out var updated))
                    rep = updated.Representative;
            }
        }
    }

    /// <summary>多 worker 匹配赛：每轮生成赛程、并行跑局、单线程按顺序合并写池。</summary>
    private static void RunMatchPhaseParallel(
        IReadOnlyDictionary<string, ComboEntry> seasonPool,
        OptimizerState state,
        Config config,
        SimulatorClass simulator,
        IItemTemplateResolver db,
        Random rng)
    {
        PrintMatchPhasePoolInfo(state, config, seasonPool);
        var seasonSigs = seasonPool.Keys.ToList();
        var playerStats = new Dictionary<string, (int games, int losses)>(StringComparer.Ordinal);
        foreach (var s in seasonSigs)
            playerStats[s] = (0, 0);

        int gamesAtSeasonStart = state.TotalGames;
        int round = 0;
        while (true)
        {
            var scheduleStart = System.Diagnostics.Stopwatch.GetTimestamp();
            var schedule = new List<(string comboSigD, string oppSig)>();
            foreach (var comboSig in seasonSigs)
            {
                var (g, l) = playerStats[comboSig];
                if (g >= config.SeasonMatchCap || l >= config.SeasonLossCap) continue;
                if (!seasonPool.TryGetValue(comboSig, out var entry)) continue;
                int toPlay = Math.Min(Math.Max(1, config.GamesPerEval), config.SeasonMatchCap - g);
                if (toPlay <= 0) continue;
                var opps = EloSystem.SelectOpponentSignaturesForSeason(state, config, seasonSigs, isNewDeck: false, entry.Elo, toPlay, rng, excludeComboSig: comboSig, useSegmentNeighborhood: true);
                opps = opps.Take(toPlay).ToList();
                foreach (var opp in opps)
                    schedule.Add((comboSig, opp));
            }
            if (schedule.Count == 0) break;
            PerfCounters.AddMatchScheduleTicks(System.Diagnostics.Stopwatch.GetTimestamp() - scheduleStart);

            round++;
            PerfCounters.AddMatchRound();
            PerfCounters.AddMatchGames(schedule.Count);

            var runStart = System.Diagnostics.Stopwatch.GetTimestamp();
            var results = RunGamesParallel(schedule, state, config, simulator, db);
            PerfCounters.AddMatchRunTicks(System.Diagnostics.Stopwatch.GetTimestamp() - runStart);

            var applyStart = System.Diagnostics.Stopwatch.GetTimestamp();
            ApplyMatchResults(state, config, results);
            PerfCounters.AddMatchApplyTicks(System.Diagnostics.Stopwatch.GetTimestamp() - applyStart);
            int seasonGames = state.TotalGames - gamesAtSeasonStart;
            if (round % 5 == 1 || schedule.Count >= 80)
                Console.WriteLine($"  匹配赛：第 {round} 轮，本轮 {schedule.Count} 场，本季累计 {seasonGames} 场，总对局 {state.TotalGames}");

            foreach (var (comboSigD, oppSig, winner) in results)
            {
                playerStats[comboSigD] = (playerStats[comboSigD].games + 1, playerStats[comboSigD].losses + (winner == 1 ? 1 : 0));
            }
        }
    }

    /// <summary>并行跑赛程中的对局，不写池；返回与 schedule 同序的 (comboSigD, oppSig, winner)。</summary>
    private static List<(string comboSigD, string oppSig, int winner)> RunGamesParallel(
        List<(string comboSigD, string oppSig)> schedule,
        OptimizerState state,
        Config config,
        SimulatorClass simulator,
        IItemTemplateResolver db)
    {
        int workers = Math.Max(1, config.Workers);
        var silentSink = new SilentBattleLogSink();
        var results = new List<(string, string, int)>(schedule.Count);

        if (workers <= 1)
        {
            foreach (var (comboSigD, oppSig) in schedule)
            {
                if (!state.TryGetEntry(comboSigD, out var eD) || !state.TryGetEntry(oppSig, out var eOpp))
                    continue;
                var tBuild0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var deckD = eD.Representative.ToDeck(db);
                var deckOpp = eOpp.Representative.ToDeck(db);
                PerfCounters.RecordDeckBuild(System.Diagnostics.Stopwatch.GetTimestamp() - tBuild0);
                int swap = ThreadLocalRandom.Next(2);
                Deck dA, dB;
                if (swap == 0) { dA = deckD; dB = deckOpp; }
                else { dA = deckOpp; dB = deckD; }
                var tRun0 = System.Diagnostics.Stopwatch.GetTimestamp();
                int winner = simulator.Run(dA, dB, db, silentSink, BattleLogLevel.None);
                PerfCounters.RecordBattleRun(System.Diagnostics.Stopwatch.GetTimestamp() - tRun0);
                if (winner >= 0 && swap == 1) winner = 1 - winner;
                results.Add((comboSigD, oppSig, winner));
            }
            return results;
        }

        var chunks = new List<List<(int index, string comboSigD, string oppSig)>>();
        for (int w = 0; w < workers; w++)
            chunks.Add(new List<(int, string, string)>());
        for (int i = 0; i < schedule.Count; i++)
        {
            var (d, opp) = schedule[i];
            chunks[i % workers].Add((i, d, opp));
        }
        var chunkResults = new List<(int index, int winner)>[workers];
        Parallel.For(0, workers, w =>
        {
            var list = new List<(int index, int winner)>();
            foreach (var (index, comboSigD, oppSig) in chunks[w])
            {
                if (!state.TryGetEntry(comboSigD, out var eD) || !state.TryGetEntry(oppSig, out var eOpp))
                    continue;
                var tBuild0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var deckD = eD.Representative.ToDeck(db);
                var deckOpp = eOpp.Representative.ToDeck(db);
                PerfCounters.RecordDeckBuild(System.Diagnostics.Stopwatch.GetTimestamp() - tBuild0);
                int swap = ThreadLocalRandom.Next(2);
                Deck dA, dB;
                if (swap == 0) { dA = deckD; dB = deckOpp; }
                else { dA = deckOpp; dB = deckD; }
                var tRun0 = System.Diagnostics.Stopwatch.GetTimestamp();
                int winner = simulator.Run(dA, dB, db, silentSink, BattleLogLevel.None);
                PerfCounters.RecordBattleRun(System.Diagnostics.Stopwatch.GetTimestamp() - tRun0);
                if (winner >= 0 && swap == 1) winner = 1 - winner;
                list.Add((index, winner));
            }
            chunkResults[w] = list;
        });
        var byIndex = new List<(int index, int winner)>();
        for (int w = 0; w < workers; w++)
            byIndex.AddRange(chunkResults[w]);
        byIndex.Sort((a, b) => a.index.CompareTo(b.index));
        foreach (var (index, winner) in byIndex)
        {
            var (d, opp) = schedule[index];
            results.Add((d, opp, winner));
        }
        return results;
    }

    /// <summary>按对局结果顺序更新池中 ELO 与对局数，不写代表排列。</summary>
    private static void ApplyMatchResults(
        OptimizerState state,
        Config config,
        List<(string comboSigD, string oppSig, int winner)> results)
    {
        var k = config.EloK;
        foreach (var (comboSigD, oppSig, winner) in results)
        {
            if (!state.TryGetEntry(comboSigD, out var entryD) || !state.TryGetEntry(oppSig, out var entryOpp))
                continue;
            double eloD = entryD.Elo;
            double eloOpp = entryOpp.Elo;
            EloSystem.UpdateElo(eloD, eloOpp, winner, k, out eloD, out eloOpp);
            if (state.VirtualPlayerPool.TryGetValue(comboSigD, out var vD))
                state.VirtualPlayerPool[comboSigD] = vD with { Elo = eloD, GameCount = vD.GameCount + 1 };
            if (state.HistoryPool.TryGetValue(comboSigD, out var hD))
                state.HistoryPool[comboSigD] = hD with { Elo = eloD, GameCount = hD.GameCount + 1 };
            if (state.VirtualPlayerPool.TryGetValue(oppSig, out var vO))
                state.VirtualPlayerPool[oppSig] = vO with { Elo = eloOpp, GameCount = vO.GameCount + 1 };
            if (state.HistoryPool.TryGetValue(oppSig, out var hO))
                state.HistoryPool[oppSig] = hO with { Elo = eloOpp, GameCount = hO.GameCount + 1 };
            state.IncrementTotalGames();
            state.RecordMatch(comboSigD, eloD, eloOpp, winner);
        }
    }

    private static int ApplyAbandon(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config, Random rng)
    {
        int threshold = Math.Max(0, config.AbandonSeasonsThreshold);
        if (threshold == 0) return 0;

        int changed = 0;
        foreach (var key in state.AnchoredPlayerComboSig.Keys.ToList())
        {
            // 放弃判定：按“该锚定玩家实际参赛的赛季数”（未参赛的赛季不计入）
            int participated = state.AnchoredParticipatedSeasonsSinceImproved.TryGetValue(key, out var p) ? p : 0;
            if (participated < threshold) continue;
            var itemName = AnchoredRepresentativeScheduler.ItemNameFromKey(key);
            if (string.IsNullOrEmpty(itemName)) continue;
            var shapeIndex = AnchoredRepresentativeScheduler.ShapeIndexFromKey(key);
            var shapePerm = Shapes.GetRandomPermutation(shapeIndex, rng);
            if (!pool.CanBuildNoDuplicate(shapePerm)) continue;
            var deck = DeckGen.RandomDeckWeightedSynergyAnchored(shapePerm, pool, state.Priors, db, rng, config.ExploreMix, config.SynergyPairLambda, itemName, config, state.TotalGames);
            if (deck == null) continue;
            var comboSig = ComboSignature.FromDeckRep(deck);
            var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, deck, state, config, pool, simulator, db, rng);
            // 放弃重启：继承该锚定玩家当前 ELO，避免赛季间活跃段位分布塌缩到最低段。
            var oldSig = state.AnchoredPlayerComboSig.TryGetValue(key, out var os) ? os : null;
            var oldElo = !string.IsNullOrEmpty(oldSig) && state.TryGetEntry(oldSig, out var oe) ? oe.Elo : config.InitialElo;
            int baseGames = GetCumulativeGameCount(state, comboSig);
            state.VirtualPlayerPool[comboSig] = new ComboEntry(comboSig, ensured.Representative, oldElo, false, ensured.IsConfirmed, baseGames);
            state.AnchoredPlayerComboSig[key] = comboSig;
            state.AnchoredLastImprovedSeason[key] = state.CurrentSeason;
            state.AnchoredParticipatedSeasonsSinceImproved[key] = 0;
            changed++;
        }
        return changed;
    }

    private static (double newElo, int gamesPlayed, int losses) RunGamesAndCountLosses(
        string comboSigD,
        DeckRep repD,
        IReadOnlyList<string> opponentSignatures,
        OptimizerState state,
        Config config,
        SimulatorClass simulator,
        IItemTemplateResolver db)
    {
        if (opponentSignatures.Count == 0)
            return (state.TryGetEntry(comboSigD, out var e) ? e.Elo : config.InitialElo, 0, 0);
        int losses = 0;
        double elo = state.TryGetEntry(comboSigD, out var entry) ? entry.Elo : config.InitialElo;
        var silentSink = new SilentBattleLogSink();
        var k = config.EloK;
        int gamesPlayed = 0;
        foreach (var oppSig in opponentSignatures)
        {
            if (!state.TryGetEntry(oppSig, out var oppEntry)) continue;
            var repOpp = oppEntry.Representative;
            var tBuild0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var deckD = repD.ToDeck(db);
            var deckOpp = repOpp.ToDeck(db);
            PerfCounters.RecordDeckBuild(System.Diagnostics.Stopwatch.GetTimestamp() - tBuild0);
            double eloOpp = oppEntry.Elo;
            int swap = Random.Shared.Next(2);
            Deck dA, dB;
            if (swap == 0) { dA = deckD; dB = deckOpp; }
            else { dA = deckOpp; dB = deckD; }
            var tRun0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int winner = simulator.Run(dA, dB, db, silentSink, BattleLogLevel.None);
            PerfCounters.RecordBattleRun(System.Diagnostics.Stopwatch.GetTimestamp() - tRun0);
            if (winner >= 0 && swap == 1) winner = 1 - winner;
            EloSystem.UpdateElo(elo, eloOpp, winner, k, out elo, out eloOpp);
            if (state.VirtualPlayerPool.TryGetValue(oppSig, out var vOpp))
                state.VirtualPlayerPool[oppSig] = vOpp with { Elo = eloOpp, GameCount = vOpp.GameCount + 1 };
            if (state.HistoryPool.TryGetValue(oppSig, out var hOpp))
                state.HistoryPool[oppSig] = hOpp with { Elo = eloOpp, GameCount = hOpp.GameCount + 1 };
            state.IncrementTotalGames();
            gamesPlayed++;
            if (winner == 1) losses++;
        }
        int baseGames = GetCumulativeGameCount(state, comboSigD);
        state.VirtualPlayerPool[comboSigD] = new ComboEntry(comboSigD, repD, elo, false, false, baseGames + gamesPlayed);
        EloSystem.TryAddToHistoryPool(state, config, comboSigD, repD, elo, gamesPlayedDelta: gamesPlayed);
        return (elo, gamesPlayed, losses);
    }

    private static void SeedPoolWithVirtualPlayers(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config, Random rng)
    {
        Console.WriteLine("[初始化] 正在生成锚定玩家...");
        var allItems = pool.SmallNames.Concat(pool.MediumNames).Concat(pool.LargeNames).ToList();
        for (int si = 0; si < Shapes.All.Count; si++)
        {
            var shape = Shapes.All[si];
            foreach (var itemName in allItems)
            {
                var t = db.GetTemplate(itemName);
                if (t == null) continue;
                int size = t.Size switch { ItemSize.Small => 1, ItemSize.Medium => 2, ItemSize.Large => 3, _ => 0 };
                if (size == 0 || !shape.Contains(size)) continue;
                var key = OptimizerState.AnchoredKey(itemName, si);
                if (state.AnchoredPlayerComboSig.ContainsKey(key)) continue;
                var shapePerm = Shapes.GetRandomPermutation(si, rng);
                if (!pool.CanBuildNoDuplicate(shapePerm)) continue;
                var deck = DeckGen.RandomDeckWeightedSynergyAnchored(shapePerm, pool, state.Priors, db, rng, config.ExploreMix, config.SynergyPairLambda, itemName, config, 0);
                if (deck == null) continue;
                var comboSig = ComboSignature.FromDeckRep(deck);
                var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, deck, state, config, pool, simulator, db, rng);
                state.VirtualPlayerPool[comboSig] = new ComboEntry(comboSig, ensured.Representative, config.InitialElo, false, ensured.IsConfirmed, 0);
                state.AnchoredPlayerComboSig[key] = comboSig;
                state.AnchoredLastImprovedSeason[key] = 0;
                state.AnchoredParticipatedSeasonsSinceImproved[key] = 0;
            }
        }
        Console.WriteLine($"  锚定玩家数: {state.AnchoredPlayerComboSig.Count}");
        Console.WriteLine("  进行初始对局...");
        foreach (var kv in state.VirtualPlayerPool.ToList())
        {
            var opps = state.VirtualPlayerPool.Keys.Where(s => s != kv.Key).Take(Math.Max(1, config.GamesPerEval)).ToList();
            if (opps.Count == 0) continue;
            EloSystem.RunGamesAndUpdateElo(kv.Key, kv.Value.Representative, opps, state, config, simulator, db);
        }
        Console.WriteLine($"  初始对局完成，总对局 {state.TotalGames}。");
    }

    private static void RestartAnchoredPlayer(
        SimulatorClass simulator,
        IItemTemplateResolver db,
        ItemPool pool,
        OptimizerState state,
        Config config,
        Random rng,
        string anchoredKey,
        string anchorItemName)
    {
        var shapeIndex = AnchoredRepresentativeScheduler.ShapeIndexFromKey(anchoredKey);
        var shapePerm = Shapes.GetRandomPermutation(shapeIndex, rng);
        if (!pool.CanBuildNoDuplicate(shapePerm)) return;

        // TODO(anchor-weighting): 初始化/重启应改为“锚定上/下游优先”的固定层级权重
        var deck = DeckGen.RandomDeckWeightedSynergyAnchored(
            shapePerm,
            pool,
            state.Priors,
            db,
            rng,
            config.ExploreMix,
            config.SynergyPairLambda,
            anchorItemName,
            config,
            state.TotalGames);
        if (deck == null) return;

        var comboSig = ComboSignature.FromDeckRep(deck);
        var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, deck, state, config, pool, simulator, db, rng);
        // 重启锚定玩家：继承该锚定玩家当前 ELO，避免下一赛季活跃玩家被大量重置到 InitialElo。
        var oldSig = state.AnchoredPlayerComboSig.TryGetValue(anchoredKey, out var os) ? os : null;
        var oldElo = !string.IsNullOrEmpty(oldSig) && state.TryGetEntry(oldSig, out var oe) ? oe.Elo : config.InitialElo;
        int baseGames = GetCumulativeGameCount(state, comboSig);
        state.VirtualPlayerPool[comboSig] = new ComboEntry(comboSig, ensured.Representative, oldElo, false, ensured.IsConfirmed, baseGames);
        state.AnchoredPlayerComboSig[anchoredKey] = comboSig;
        state.AnchoredLastImprovedSeason[anchoredKey] = state.CurrentSeason;
        state.AnchoredParticipatedSeasonsSinceImproved[anchoredKey] = 0;
        state.IncrementTotalRestarts();
    }

}
