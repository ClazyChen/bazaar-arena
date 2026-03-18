using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena.QualityDeckFinder;

/// <summary>主循环：虚拟赛季（代表选择 → 匹配赛 → 卡组优化 → 注入/放弃）＋周期性 Top10 与状态保存。</summary>
public static class Runner
{
    public static void Run(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config)
    {
        var rng = state.RngSeed.HasValue ? new Random(state.RngSeed.Value) : new Random();

        if (state.Pool.Count == 0 || (state.AnchoredPlayerComboSig.Count == 0 && state.StrengthPlayerComboSigs.Count == 0))
        {
            SeedPoolWithVirtualPlayers(simulator, db, pool, state, config, rng);
            Console.WriteLine($"[初始化完成] 池大小 {state.Pool.Count}，开始虚拟赛季循环。");
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
        var representatives = AnchoredRepresentativeScheduler.SelectRepresentatives(state, config, rng);
        var activeComboSigs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in state.StrengthPlayerComboSigs)
            activeComboSigs.Add(s);
        foreach (var key in representatives.Values)
        {
            if (state.AnchoredPlayerComboSig.TryGetValue(key, out var sig))
                activeComboSigs.Add(sig);
        }

        if (activeComboSigs.Count == 0) return;

        Console.WriteLine($"[赛季 {state.CurrentSeason + 1}] 活跃玩家 {activeComboSigs.Count}，总对局 {state.TotalGames}，开始匹配赛...");

        // 匹配赛：受 SeasonMatchCap / SeasonLossCap 限制；多 worker 时每轮并行跑局、单线程合并写池
        if (config.Workers > 0)
            RunMatchPhaseParallel(activeComboSigs.ToList(), state, config, simulator, db, rng);
        else
            RunMatchPhaseSingle(activeComboSigs.ToList(), state, config, simulator, db, rng);

        Console.WriteLine("  匹配赛结束。");

        // 卡组优化：每个活跃玩家尝试一次爬山，若更优则切换并本季不再优化；强度玩家同卡组合并
        Console.WriteLine($"  卡组优化：强度玩家 {state.StrengthPlayerComboSigs.Count}，锚定代表 {representatives.Count}。");
        var strengthIndicesToUpdate = new List<(int index, string newComboSig)>();
        int idx = 0;
        foreach (var comboSig in state.StrengthPlayerComboSigs.ToList())
        {
            if (!state.Pool.TryGetValue(comboSig, out var entry)) { idx++; continue; }
            var (newSig, newRep, isLocalOpt) = HillClimb.Run(comboSig, entry.Representative, state, config, pool, simulator, db, rng, anchorItemName: null);
            if (string.IsNullOrEmpty(newSig)) { idx++; continue; }
            var newElo = state.Pool.TryGetValue(newSig, out var ne) ? ne.Elo : config.InitialElo;
            if (newSig != comboSig && newElo > entry.Elo)
            {
                strengthIndicesToUpdate.Add((idx, newSig));
                EloSystem.TryAddToPool(state, config, newSig, newRep, newElo, isLocalOptimum: isLocalOpt);
            }
            idx++;
        }
        while (state.StrengthLastImprovedSeason.Count < state.StrengthPlayerComboSigs.Count)
            state.StrengthLastImprovedSeason.Add(0);
        foreach (var pair in strengthIndicesToUpdate)
        {
            if (pair.index < state.StrengthPlayerComboSigs.Count)
            {
                state.StrengthPlayerComboSigs[pair.index] = pair.newComboSig;
                state.StrengthLastImprovedSeason[pair.index] = state.CurrentSeason;
            }
        }
        Console.WriteLine("    强度玩家优化完成。");
        foreach (var itemName in representatives.Keys.ToList())
        {
            var key = representatives[itemName];
            if (!state.AnchoredPlayerComboSig.TryGetValue(key, out var comboSig)) continue;
            if (!state.Pool.TryGetValue(comboSig, out var entry)) continue;
            var anchorItem = AnchoredRepresentativeScheduler.ItemNameFromKey(key);
            var (newSig, newRep, _) = HillClimb.Run(comboSig, entry.Representative, state, config, pool, simulator, db, rng, anchorItemName: anchorItem);
            if (string.IsNullOrEmpty(newSig)) continue;
            var newElo = state.Pool.TryGetValue(newSig, out var ne) ? ne.Elo : config.InitialElo;
            if (newSig != comboSig && newElo > entry.Elo)
            {
                state.AnchoredPlayerComboSig[key] = newSig;
                state.AnchoredLastImprovedSeason[key] = state.CurrentSeason;
                EloSystem.TryAddToPool(state, config, newSig, newRep, newElo);
            }
        }
        Console.WriteLine("    锚定代表优化完成。");
        var merged = state.StrengthPlayerComboSigs.Distinct(StringComparer.Ordinal).ToList();
        var newLast = new List<int>();
        foreach (var sig in merged)
        {
            int best = 0;
            for (int i = 0; i < state.StrengthPlayerComboSigs.Count; i++)
                if (StringComparer.Ordinal.Equals(state.StrengthPlayerComboSigs[i], sig) && i < state.StrengthLastImprovedSeason.Count)
                    best = Math.Max(best, state.StrengthLastImprovedSeason[i]);
            newLast.Add(best);
        }
        state.StrengthPlayerComboSigs.Clear();
        state.StrengthPlayerComboSigs.AddRange(merged);
        state.StrengthLastImprovedSeason.Clear();
        state.StrengthLastImprovedSeason.AddRange(newLast);

        ApplyAbandon(simulator, db, pool, state, config, rng);

        if (config.InjectInterval > 0 && state.CurrentSeason > 0 && state.CurrentSeason % config.InjectInterval == 0)
            InjectStrengthPlayers(simulator, db, pool, state, config, rng);
    }

    private static void PrintMatchPhasePoolInfo(OptimizerState state, Config config)
    {
        int maxSeg;
        lock (config.SegmentBoundsLock)
        {
            maxSeg = config.SegmentBounds.Count;
        }
        var segCounts = new List<int>();
        for (int s = 0; s <= maxSeg; s++)
            segCounts.Add(state.SignaturesInSegment(s).Count);
        Console.WriteLine($"  匹配赛池：池大小 {state.Pool.Count}，各段人数 [{string.Join(", ", segCounts)}]");
    }

    /// <summary>单线程匹配赛：逐玩家、逐批对局并立即更新池。</summary>
    private static void RunMatchPhaseSingle(
        List<string> activeComboSigs,
        OptimizerState state,
        Config config,
        SimulatorClass simulator,
        IItemTemplateResolver db,
        Random rng)
    {
        PrintMatchPhasePoolInfo(state, config);
        const int progressInterval = 50;
        int processed = 0;
        foreach (var comboSig in activeComboSigs)
        {
            processed++;
            if (processed % progressInterval == 0)
                Console.WriteLine($"  匹配赛：已处理 {processed}/{activeComboSigs.Count} 个活跃玩家，总对局 {state.TotalGames}");
            if (!state.Pool.TryGetValue(comboSig, out var entry)) continue;
            var rep = entry.Representative;
            double elo = entry.Elo;
            int games = 0;
            int losses = 0;
            while (games < config.SeasonMatchCap && losses < config.SeasonLossCap)
            {
                var opps = EloSystem.SelectOpponentSignatures(state, config, isNewDeck: false, elo, Math.Max(1, config.GamesPerEval), rng, excludeComboSig: comboSig, useSegmentNeighborhood: true);
                opps = opps.Take(Math.Max(1, config.GamesPerEval)).ToList();
                if (opps.Count == 0) break;
                var (newElo, gamesPlayed, lossDelta) = RunGamesAndCountLosses(comboSig, rep, opps, state, config, simulator, db);
                elo = newElo;
                games += gamesPlayed;
                losses += lossDelta;
                if (state.Pool.TryGetValue(comboSig, out var updated))
                    rep = updated.Representative;
            }
        }
    }

    /// <summary>多 worker 匹配赛：每轮生成赛程、并行跑局、单线程按顺序合并写池。</summary>
    private static void RunMatchPhaseParallel(
        List<string> activeComboSigs,
        OptimizerState state,
        Config config,
        SimulatorClass simulator,
        IItemTemplateResolver db,
        Random rng)
    {
        PrintMatchPhasePoolInfo(state, config);
        var playerStats = new Dictionary<string, (int games, int losses)>(StringComparer.Ordinal);
        foreach (var s in activeComboSigs)
            playerStats[s] = (0, 0);

        int gamesAtSeasonStart = state.TotalGames;
        int round = 0;
        while (true)
        {
            var schedule = new List<(string comboSigD, string oppSig)>();
            foreach (var comboSig in activeComboSigs)
            {
                var (g, l) = playerStats[comboSig];
                if (g >= config.SeasonMatchCap || l >= config.SeasonLossCap) continue;
                if (!state.Pool.TryGetValue(comboSig, out var entry)) continue;
                int toPlay = Math.Min(Math.Max(1, config.GamesPerEval), config.SeasonMatchCap - g);
                if (toPlay <= 0) continue;
                var opps = EloSystem.SelectOpponentSignatures(state, config, isNewDeck: false, entry.Elo, toPlay, rng, excludeComboSig: comboSig, useSegmentNeighborhood: true);
                opps = opps.Take(toPlay).ToList();
                foreach (var opp in opps)
                    schedule.Add((comboSig, opp));
            }
            if (schedule.Count == 0) break;

            round++;
            var results = RunGamesParallel(schedule, state, config, simulator, db);
            ApplyMatchResults(state, config, results);
            int seasonGames = state.TotalGames - gamesAtSeasonStart;
            if (round % 5 == 1 || schedule.Count >= 80)
                Console.WriteLine($"  匹配赛：第 {round} 轮，本轮 {schedule.Count} 场，本季累计 {seasonGames} 场，总对局 {state.TotalGames}");

            foreach (var (comboSigD, oppSig, winner) in results)
            {
                playerStats[comboSigD] = (playerStats[comboSigD].games + 1, playerStats[comboSigD].losses + (winner == 1 ? 1 : 0));
                if (playerStats.TryGetValue(oppSig, out var oppStat))
                    playerStats[oppSig] = (oppStat.games + 1, oppStat.losses + (winner == 0 ? 1 : 0));
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
                if (!state.Pool.TryGetValue(comboSigD, out var eD) || !state.Pool.TryGetValue(oppSig, out var eOpp))
                    continue;
                var deckD = eD.Representative.ToDeck(db);
                var deckOpp = eOpp.Representative.ToDeck(db);
                int swap = Random.Shared.Next(2);
                Deck dA, dB;
                if (swap == 0) { dA = deckD; dB = deckOpp; }
                else { dA = deckOpp; dB = deckD; }
                int winner = simulator.Run(dA, dB, db, silentSink, BattleLogLevel.None);
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
            var rnd = new Random(Random.Shared.Next());
            foreach (var (index, comboSigD, oppSig) in chunks[w])
            {
                if (!state.Pool.TryGetValue(comboSigD, out var eD) || !state.Pool.TryGetValue(oppSig, out var eOpp))
                    continue;
                var deckD = eD.Representative.ToDeck(db);
                var deckOpp = eOpp.Representative.ToDeck(db);
                int swap = rnd.Next(2);
                Deck dA, dB;
                if (swap == 0) { dA = deckD; dB = deckOpp; }
                else { dA = deckOpp; dB = deckD; }
                int winner = simulator.Run(dA, dB, db, silentSink, BattleLogLevel.None);
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
            if (!state.Pool.TryGetValue(comboSigD, out var entryD) || !state.Pool.TryGetValue(oppSig, out var entryOpp))
                continue;
            double eloD = entryD.Elo;
            double eloOpp = entryOpp.Elo;
            EloSystem.UpdateElo(eloD, eloOpp, winner, k, out eloD, out eloOpp);
            state.Pool[comboSigD] = entryD with { Elo = eloD, GameCount = entryD.GameCount + 1 };
            state.Pool[oppSig] = entryOpp with { Elo = eloOpp, GameCount = entryOpp.GameCount + 1 };
            state.IncrementTotalGames();
            state.RecordMatch(comboSigD, eloD, eloOpp, winner);
        }
    }

    private static void ApplyAbandon(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config, Random rng)
    {
        int threshold = Math.Max(0, config.AbandonSeasonsThreshold);
        if (threshold == 0) return;

        for (int i = state.StrengthPlayerComboSigs.Count - 1; i >= 0; i--)
        {
            if (i >= state.StrengthLastImprovedSeason.Count) continue;
            if (state.CurrentSeason - state.StrengthLastImprovedSeason[i] >= threshold)
            {
                state.StrengthPlayerComboSigs.RemoveAt(i);
                state.StrengthLastImprovedSeason.RemoveAt(i);
            }
        }

        foreach (var key in state.AnchoredPlayerComboSig.Keys.ToList())
        {
            int lastImproved = state.AnchoredLastImprovedSeason.GetValueOrDefault(key, 0);
            if (state.CurrentSeason - lastImproved < threshold) continue;
            var itemName = AnchoredRepresentativeScheduler.ItemNameFromKey(key);
            if (string.IsNullOrEmpty(itemName)) continue;
            var shapeIndex = AnchoredRepresentativeScheduler.ShapeIndexFromKey(key);
            var shapePerm = Shapes.GetRandomPermutation(shapeIndex, rng);
            if (!pool.CanBuildNoDuplicate(shapePerm)) continue;
            var deck = DeckGen.RandomDeckWeightedSynergyAnchored(shapePerm, pool, state.Priors, db, rng, config.ExploreMix, config.SynergyPairLambda, itemName, config, state.TotalGames);
            if (deck == null) continue;
            var comboSig = ComboSignature.FromDeckRep(deck);
            var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, deck, state, config, pool, simulator, db, rng);
            EloSystem.TryAddToPool(state, config, comboSig, ensured.Representative, config.InitialElo, isConfirmed: ensured.IsConfirmed);
            state.AnchoredPlayerComboSig[key] = comboSig;
            state.AnchoredLastImprovedSeason[key] = state.CurrentSeason;
        }
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
            return (state.Pool.TryGetValue(comboSigD, out var e) ? e.Elo : config.InitialElo, 0, 0);
        int losses = 0;
        double elo = state.Pool.TryGetValue(comboSigD, out var entry) ? entry.Elo : config.InitialElo;
        var silentSink = new SilentBattleLogSink();
        var k = config.EloK;
        int gamesPlayed = 0;
        foreach (var oppSig in opponentSignatures)
        {
            if (!state.Pool.TryGetValue(oppSig, out var oppEntry)) continue;
            var repOpp = oppEntry.Representative;
            var deckD = repD.ToDeck(db);
            var deckOpp = repOpp.ToDeck(db);
            double eloOpp = oppEntry.Elo;
            int swap = Random.Shared.Next(2);
            Deck dA, dB;
            if (swap == 0) { dA = deckD; dB = deckOpp; }
            else { dA = deckOpp; dB = deckD; }
            int winner = simulator.Run(dA, dB, db, silentSink, BattleLogLevel.None);
            if (winner >= 0 && swap == 1) winner = 1 - winner;
            EloSystem.UpdateElo(elo, eloOpp, winner, k, out elo, out eloOpp);
            state.Pool[oppSig] = oppEntry with { Elo = eloOpp, GameCount = oppEntry.GameCount + 1 };
            state.IncrementTotalGames();
            gamesPlayed++;
            if (winner == 1) losses++;
        }
        EloSystem.TryAddToPool(state, config, comboSigD, repD, elo, gamesPlayedDelta: gamesPlayed);
        return (elo, gamesPlayed, losses);
    }

    private static void SeedPoolWithVirtualPlayers(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config, Random rng)
    {
        Console.WriteLine("[初始化] 正在生成锚定玩家与强度玩家...");
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
                EloSystem.TryAddToPool(state, config, comboSig, ensured.Representative, config.InitialElo, isConfirmed: ensured.IsConfirmed);
                state.AnchoredPlayerComboSig[key] = comboSig;
                state.AnchoredLastImprovedSeason[key] = 0;
            }
        }
        for (int k = 0; k < 10; k++)
        {
            var shapeIndex = rng.Next(Shapes.All.Count);
            var shape = Shapes.GetRandomPermutation(shapeIndex, rng);
            if (!pool.CanBuildNoDuplicate(shape)) continue;
            var deck = DeckGen.RandomDeckWeightedSynergy(shape, pool, state.Priors, db, rng, config.ExploreMix, config.SynergyPairLambda, config, 0);
            if (deck == null) continue;
            var comboSig = ComboSignature.FromDeckRep(deck);
            if (state.Pool.ContainsKey(comboSig)) continue;
            var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, deck, state, config, pool, simulator, db, rng);
            EloSystem.TryAddToPool(state, config, comboSig, ensured.Representative, config.InitialElo, isConfirmed: ensured.IsConfirmed);
            state.StrengthPlayerComboSigs.Add(comboSig);
            state.StrengthLastImprovedSeason.Add(0);
        }
        Console.WriteLine($"  锚定玩家数: {state.AnchoredPlayerComboSig.Count}，强度玩家数: {state.StrengthPlayerComboSigs.Count}");
        Console.WriteLine("  进行初始对局...");
        foreach (var kv in state.Pool.ToList())
        {
            var opps = state.Pool.Keys.Where(s => s != kv.Key).Take(Math.Max(1, config.GamesPerEval)).ToList();
            if (opps.Count == 0) continue;
            EloSystem.RunGamesAndUpdateElo(kv.Key, kv.Value.Representative, opps, state, config, simulator, db);
        }
        Console.WriteLine($"  初始对局完成，总对局 {state.TotalGames}。");
    }

    private static void InjectStrengthPlayers(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config, Random rng)
    {
        int added = 0;
        int tries = Math.Max(config.InjectCount * 3, 10);
        for (int t = 0; t < tries && added < config.InjectCount; t++)
        {
            var shapeIndex = rng.Next(Shapes.All.Count);
            var shape = Shapes.GetRandomPermutation(shapeIndex, rng);
            if (!pool.CanBuildNoDuplicate(shape)) continue;
            var deck = DeckGen.RandomDeckWeightedSynergy(shape, pool, state.Priors, db, rng, config.ExploreMix, config.SynergyPairLambda, config, state.TotalGames);
            if (deck == null) continue;
            var comboSig = ComboSignature.FromDeckRep(deck);
            if (state.Pool.ContainsKey(comboSig)) continue;
            var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, deck, state, config, pool, simulator, db, rng);
            var opps = EloSystem.SelectOpponentSignatures(state, config, isNewDeck: true, null, Math.Max(1, config.GamesPerEval * 2));
            if (opps.Count == 0)
                EloSystem.TryAddToPool(state, config, comboSig, ensured.Representative, config.InitialElo, isConfirmed: ensured.IsConfirmed);
            else
                EloSystem.RunGamesAndUpdateElo(comboSig, ensured.Representative, opps, state, config, simulator, db);
            state.StrengthPlayerComboSigs.Add(comboSig);
            state.StrengthLastImprovedSeason.Add(state.CurrentSeason);
            added++;
        }
    }

}
