using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena.QualityDeckFinder;

/// <summary>ELO 更新、按段抽样对手、新卡组先打段 0、入池时若段满则按与同段相似度踢出最冗余者以保护多样性。</summary>
public static class EloSystem
{
    private static int GetCumulativeGameCount(OptimizerState state, string comboSig)
    {
        int v = state.VirtualPlayerPool.TryGetValue(comboSig, out var ve) ? ve.GameCount : 0;
        int h = state.HistoryPool.TryGetValue(comboSig, out var he) ? he.GameCount : 0;
        return Math.Max(v, h);
    }

    /// <summary>两张组合（以代表排列取物品集合）物品集合的 Jaccard 相似度（0~1），用于跨形状比较。</summary>
    private static double Similarity(DeckRep aRep, DeckRep bRep)
    {
        var setA = new HashSet<string>(aRep.ItemNames, StringComparer.Ordinal);
        var setB = new HashSet<string>(bRep.ItemNames, StringComparer.Ordinal);
        int cap = setA.Intersect(setB).Count();
        int cup = setA.Union(setB).Count();
        return cup == 0 ? 0 : (double)cap / cup;
    }

    /// <summary>某卡组在该段内的冗余度：与段内其余卡组相似度之和；越高表示与同段越重复。</summary>
    private static double RedundancyInSegment(string sig, IReadOnlyList<string> segmentSigs, OptimizerState state)
    {
        if (!state.HistoryPool.TryGetValue(sig, out var entry)) return 0;
        var rep = entry.Representative;
        double sum = 0;
        foreach (var other in segmentSigs)
        {
            if (other == sig) continue;
            if (!state.HistoryPool.TryGetValue(other, out var otherEntry)) continue;
            sum += Similarity(rep, otherEntry.Representative);
        }
        return sum;
    }
    /// <summary>计算 ELO 期望得分：己方 eloA 对 己方 eloB 的期望得分（0~1）。</summary>
    public static double ExpectedScore(double eloA, double eloB)
    {
        return 1.0 / (1.0 + Math.Pow(10, (eloB - eloA) / 400.0));
    }

    /// <summary>根据对战结果更新 ELO：winner 0 或 1 表示哪方胜，-1 平局。K 因子。</summary>
    public static void UpdateElo(double eloA, double eloB, int winner, double k, out double newEloA, out double newEloB)
    {
        var expA = ExpectedScore(eloA, eloB);
        var expB = 1.0 - expA;
        double actualA, actualB;
        if (winner < 0) { actualA = 0.5; actualB = 0.5; }
        else if (winner == 0) { actualA = 1; actualB = 0; }
        else { actualA = 0; actualB = 1; }
        newEloA = eloA + k * (actualA - expA);
        newEloB = eloB + k * (actualB - expB);
    }

    /// <summary>
    /// 为卡组 D 选取对手签名列表：
    /// - 新卡组：从段 0 起往高段抽直到够 M 个
    /// - 非新卡组：默认从 D 所在段往低段抽；useSegmentNeighborhood 为 true 时从当前段±1 段抽
    /// excludeComboSig 不为空时排除该签名，便于匹配赛不浪费名额。
    /// </summary>
    public static List<string> SelectOpponentSignatures(OptimizerState state, Config config, bool isNewDeck, double? deckElo, int M, Random? rng = null, string? excludeComboSig = null, bool useSegmentNeighborhood = false)
    {
        rng ??= Random.Shared;
        var segmentIndex = isNewDeck ? 0 : state.SegmentIndex(deckElo ?? config.InitialElo);
        var candidates = new List<string>();
        int maxSeg;
        lock (config.SegmentBoundsLock)
        {
            maxSeg = config.SegmentBounds.Count;
        }

        void AddFromSegment(int s)
        {
            var inSeg = state.SignaturesInSegment(state.HistoryPool, s);
            foreach (var sig in inSeg.OrderBy(_ => rng.Next()))
            {
                if (excludeComboSig != null && string.Equals(sig, excludeComboSig, StringComparison.Ordinal)) continue;
                candidates.Add(sig);
                if (candidates.Count >= M) return;
            }
        }

        if (isNewDeck)
        {
            for (int s = 0; s <= maxSeg && candidates.Count < M; s++)
                AddFromSegment(s);
        }
        else if (useSegmentNeighborhood)
        {
            var lo = Math.Max(0, segmentIndex - 1);
            var hi = Math.Min(maxSeg, segmentIndex + 1);
            for (int s = lo; s <= hi && candidates.Count < M; s++)
                AddFromSegment(s);
        }
        else
        {
            for (int s = segmentIndex; s >= 0 && candidates.Count < M; s--)
                AddFromSegment(s);
        }
        return candidates.Distinct(StringComparer.Ordinal).Take(M).ToList();
    }

    /// <summary>
    /// 为本赛季匹配赛选取对手签名列表：候选来自「历史池 + 本赛季参赛虚拟玩家（activeComboSigs）」的并集。
    /// </summary>
    public static List<string> SelectOpponentSignaturesForSeason(
        OptimizerState state,
        Config config,
        IReadOnlyCollection<string> activeComboSigs,
        bool isNewDeck,
        double? deckElo,
        int M,
        Random? rng = null,
        string? excludeComboSig = null,
        bool useSegmentNeighborhood = false)
    {
        rng ??= Random.Shared;
        M = Math.Max(0, M);
        if (M == 0) return [];

        var segmentIndex = isNewDeck ? 0 : state.SegmentIndex(deckElo ?? config.InitialElo);
        int maxSeg;
        lock (config.SegmentBoundsLock)
        {
            maxSeg = config.SegmentBounds.Count;
        }

        // 建一个按段位分桶的并集快照（只包含可查到条目的签名）
        var segBuckets = new List<List<string>>(maxSeg + 1);
        for (int i = 0; i <= maxSeg; i++) segBuckets.Add(new List<string>());

        void AddSig(string sig)
        {
            if (excludeComboSig != null && string.Equals(sig, excludeComboSig, StringComparison.Ordinal))
                return;
            if (!state.TryGetEntry(sig, out var e))
                return;
            int seg = state.SegmentIndex(e.Elo);
            if (seg < 0) seg = 0;
            if (seg > maxSeg) seg = maxSeg;
            segBuckets[seg].Add(sig);
        }

        // 历史池 + 活跃参赛玩家并集
        foreach (var sig in state.HistoryPool.Keys)
            AddSig(sig);
        foreach (var sig in activeComboSigs)
            AddSig(sig);

        IEnumerable<int> SegmentOrder()
        {
            if (isNewDeck)
            {
                for (int s = 0; s <= maxSeg; s++) yield return s;
                yield break;
            }
            if (useSegmentNeighborhood)
            {
                var lo = Math.Max(0, segmentIndex - 1);
                var hi = Math.Min(maxSeg, segmentIndex + 1);
                for (int s = lo; s <= hi; s++) yield return s;
                yield break;
            }
            for (int s = segmentIndex; s >= 0; s--) yield return s;
        }

        var picked = new List<string>(M);
        foreach (var s in SegmentOrder())
        {
            foreach (var sig in segBuckets[s].OrderBy(_ => rng.Next()))
            {
                picked.Add(sig);
                if (picked.Count >= M) break;
            }
            if (picked.Count >= M) break;
        }

        return picked.Distinct(StringComparer.Ordinal).Take(M).ToList();
    }

    /// <summary>运行若干场对战：组合代表 repD 与 opponents 中随机对手的代表对战，更新双方组合的 ELO；返回 D 的更新后 ELO。</summary>
    public static double RunGamesAndUpdateElo(
        string comboSigD,
        DeckRep repD,
        IReadOnlyList<string> opponentSignatures,
        OptimizerState state,
        Config config,
        SimulatorClass simulator,
        IItemTemplateResolver db)
    {
        if (opponentSignatures.Count == 0)
            return state.TryGetEntry(comboSigD, out var e) ? e.Elo : config.InitialElo;

        var silentSink = new SilentBattleLogSink();
        var k = config.EloK;
        var gamesPerOpp = Math.Max(1, config.GamesPerEval / Math.Max(1, opponentSignatures.Count));
        double currentEloD = state.TryGetEntry(comboSigD, out var entryD) ? entryD.Elo : config.InitialElo;
        int gamesPlayedByD = 0;

        int workers = Math.Max(0, config.Workers);

        // 先把一次评估需要跑的所有 games 按“原本的顺序”展开成线性列表：
        // (oppSig0 的 g=0.., oppSig1 的 g=0.., ...)
        // 然后并行跑出 winners，再按该线性顺序单线程应用 ELO/写池，保持语义不变。
        var tBuildD0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var deckD = repD.ToDeck(db);
        PerfCounters.RecordDeckBuild(System.Diagnostics.Stopwatch.GetTimestamp() - tBuildD0);

        var games = new List<(string oppSig, Deck deckOpp, int swap)>(opponentSignatures.Count * gamesPerOpp);
        foreach (var oppSig in opponentSignatures)
        {
            if (!state.TryGetEntry(oppSig, out var oppEntry)) continue;
            var repOpp = oppEntry.Representative;
            var tBuild0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var deckOpp = repOpp.ToDeck(db);
            PerfCounters.RecordDeckBuild(System.Diagnostics.Stopwatch.GetTimestamp() - tBuild0);

            for (int g = 0; g < gamesPerOpp; g++)
                games.Add((oppSig, deckOpp, Random.Shared.Next(2)));
        }

        var winners = workers <= 1
            ? SimulateWinnersSingle(simulator, db, silentSink, deckD, games)
            : SimulateWinnersParallel(simulator, db, silentSink, deckD, games, workers);

        // 顺序应用（与 games 列表顺序一致）
        var currentOppElo = new Dictionary<string, double>(StringComparer.Ordinal);
        for (int i = 0; i < games.Count; i++)
        {
            var (oppSig, _, swap) = games[i];
            if (!currentOppElo.TryGetValue(oppSig, out var eloOpp))
            {
                if (!state.TryGetEntry(oppSig, out var oppEntry)) continue;
                eloOpp = oppEntry.Elo;
                currentOppElo[oppSig] = eloOpp;
            }

            int winner = winners[i];
            if (winner >= 0 && swap == 1) winner = 1 - winner;
            UpdateElo(currentEloD, eloOpp, winner, k, out currentEloD, out eloOpp);
            currentOppElo[oppSig] = eloOpp;

            if (state.HistoryPool.TryGetValue(oppSig, out var hOpp))
                state.HistoryPool[oppSig] = hOpp with { Elo = eloOpp, GameCount = hOpp.GameCount + 1 };
            if (state.VirtualPlayerPool.TryGetValue(oppSig, out var vOpp))
                state.VirtualPlayerPool[oppSig] = vOpp with { Elo = eloOpp, GameCount = vOpp.GameCount + 1 };
            state.IncrementTotalGames();
            gamesPlayedByD++;
            state.RecordMatch(comboSigD, currentEloD, eloOpp, winner);
        }

        // D 的最新 elo 写回虚拟玩家池；并尝试写入历史池（受分段上限限制）
        int baseGames = GetCumulativeGameCount(state, comboSigD);
        state.VirtualPlayerPool[comboSigD] = new ComboEntry(comboSigD, repD, currentEloD, false, false, baseGames + gamesPlayedByD);
        TryAddToHistoryPool(state, config, comboSigD, repD, currentEloD, gamesPlayedDelta: gamesPlayedByD);
        return currentEloD;
    }

    /// <summary>
    /// 按总预算复测若干局：将 totalGamesBudget 尽量均匀分配到 opponents 上；更新双方 ELO 并返回 D 的更新后 ELO。
    /// 用于“池内复测/联赛”，避免复用 GamesPerEval 的语义。
    /// </summary>
    public static double RunGamesAndUpdateEloBudget(
        string comboSigD,
        DeckRep repD,
        IReadOnlyList<string> opponentSignatures,
        int totalGamesBudget,
        OptimizerState state,
        Config config,
        SimulatorClass simulator,
        IItemTemplateResolver db,
        Random? rng = null)
    {
        rng ??= Random.Shared;
        totalGamesBudget = Math.Max(0, totalGamesBudget);
        if (opponentSignatures.Count == 0 || totalGamesBudget == 0)
            return state.TryGetEntry(comboSigD, out var e) ? e.Elo : config.InitialElo;

        var silentSink = new SilentBattleLogSink();
        var k = config.EloK;
        double currentEloD = state.TryGetEntry(comboSigD, out var entryD) ? entryD.Elo : config.InitialElo;
        int gamesPlayedByD = 0;

        var opps = opponentSignatures.Distinct(StringComparer.Ordinal).ToList();
        int oppCount = Math.Max(1, opps.Count);
        int gamesPerOpp = (int)Math.Ceiling(totalGamesBudget / (double)oppCount);

        int workers = Math.Max(0, config.Workers);
        var tBuildD0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var deckD = repD.ToDeck(db);
        PerfCounters.RecordDeckBuild(System.Diagnostics.Stopwatch.GetTimestamp() - tBuildD0);

        var games = new List<(string oppSig, Deck deckOpp, int swap)>(Math.Max(8, totalGamesBudget));
        foreach (var oppSig in opps)
        {
            if (games.Count >= totalGamesBudget) break;
            if (!state.TryGetEntry(oppSig, out var oppEntry)) continue;
            var repOpp = oppEntry.Representative;
            var tBuild0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var deckOpp = repOpp.ToDeck(db);
            PerfCounters.RecordDeckBuild(System.Diagnostics.Stopwatch.GetTimestamp() - tBuild0);

            int gCap = Math.Min(gamesPerOpp, Math.Max(0, totalGamesBudget - games.Count));
            for (int g = 0; g < gCap; g++)
                games.Add((oppSig, deckOpp, rng.Next(2)));
        }

        var winners = workers <= 1
            ? SimulateWinnersSingle(simulator, db, silentSink, deckD, games)
            : SimulateWinnersParallel(simulator, db, silentSink, deckD, games, workers);

        var currentOppElo = new Dictionary<string, double>(StringComparer.Ordinal);
        for (int i = 0; i < games.Count; i++)
        {
            var (oppSig, _, swap) = games[i];
            if (!currentOppElo.TryGetValue(oppSig, out var eloOpp))
            {
                if (!state.TryGetEntry(oppSig, out var oppEntry)) continue;
                eloOpp = oppEntry.Elo;
                currentOppElo[oppSig] = eloOpp;
            }

            int winner = winners[i];
            if (winner >= 0 && swap == 1) winner = 1 - winner;
            UpdateElo(currentEloD, eloOpp, winner, k, out currentEloD, out eloOpp);
            currentOppElo[oppSig] = eloOpp;

            if (state.HistoryPool.TryGetValue(oppSig, out var hOpp))
                state.HistoryPool[oppSig] = hOpp with { Elo = eloOpp, GameCount = hOpp.GameCount + 1 };
            if (state.VirtualPlayerPool.TryGetValue(oppSig, out var vOpp))
                state.VirtualPlayerPool[oppSig] = vOpp with { Elo = eloOpp, GameCount = vOpp.GameCount + 1 };
            state.IncrementTotalGames();
            gamesPlayedByD++;
            state.RecordMatch(comboSigD, currentEloD, eloOpp, winner);
        }

        int baseGames = GetCumulativeGameCount(state, comboSigD);
        state.VirtualPlayerPool[comboSigD] = new ComboEntry(comboSigD, repD, currentEloD, false, false, baseGames + gamesPlayedByD);
        TryAddToHistoryPool(state, config, comboSigD, repD, currentEloD, gamesPlayedDelta: gamesPlayedByD);
        return currentEloD;
    }

    private static int[] SimulateWinnersSingle(
        SimulatorClass simulator,
        IItemTemplateResolver db,
        IBattleLogSink silentSink,
        Deck deckD,
        List<(string oppSig, Deck deckOpp, int swap)> games)
    {
        var winners = new int[games.Count];
        for (int i = 0; i < games.Count; i++)
        {
            var (_, deckOpp, swap) = games[i];
            Deck dA = swap == 0 ? deckD : deckOpp;
            Deck dB = swap == 0 ? deckOpp : deckD;
            var tRun0 = System.Diagnostics.Stopwatch.GetTimestamp();
            winners[i] = simulator.Run(dA, dB, db, silentSink, BattleLogLevel.None);
            PerfCounters.RecordBattleRun(System.Diagnostics.Stopwatch.GetTimestamp() - tRun0);
        }
        return winners;
    }

    private static int[] SimulateWinnersParallel(
        SimulatorClass simulator,
        IItemTemplateResolver db,
        IBattleLogSink silentSink,
        Deck deckD,
        List<(string oppSig, Deck deckOpp, int swap)> games,
        int workers)
    {
        workers = Math.Max(1, workers);
        var winners = new int[games.Count];
        var chunks = new List<List<int>>(workers);
        for (int w = 0; w < workers; w++) chunks.Add(new List<int>());
        for (int i = 0; i < games.Count; i++) chunks[i % workers].Add(i);

        Parallel.For(0, workers, w =>
        {
            foreach (var i in chunks[w])
            {
                var (_, deckOpp, swap) = games[i];
                Deck dA = swap == 0 ? deckD : deckOpp;
                Deck dB = swap == 0 ? deckOpp : deckD;
                var tRun0 = System.Diagnostics.Stopwatch.GetTimestamp();
                winners[i] = simulator.Run(dA, dB, db, silentSink, BattleLogLevel.None);
                PerfCounters.RecordBattleRun(System.Diagnostics.Stopwatch.GetTimestamp() - tRun0);
            }
        });

        return winners;
    }

    /// <summary>将组合加入历史池：按 ELO 归段，若段满则踢出该段内与同段最相似（最冗余）且 ELO 低于新组合的一张，以保护多样性。</summary>
    public static void TryAddToHistoryPool(
        OptimizerState state,
        Config config,
        string comboSig,
        DeckRep representative,
        double elo,
        bool isLocalOptimum = false,
        bool isConfirmed = false,
        int gamesPlayedDelta = 0)
    {
        lock (state.HistoryPoolSync)
        {
            var segmentIndex = state.SegmentIndex(elo);
            var inSegment = state.SignaturesInSegment(state.HistoryPool, segmentIndex);
            if (state.HistoryPool.TryGetValue(comboSig, out var existing))
            {
                state.HistoryPool[comboSig] = existing with
                {
                    Representative = representative,
                    Elo = elo,
                    IsLocalOptimum = existing.IsLocalOptimum || isLocalOptimum,
                    IsConfirmed = existing.IsConfirmed || isConfirmed,
                    GameCount = existing.GameCount + Math.Max(0, gamesPlayedDelta),
                };
                return;
            }
            if (inSegment.Count >= config.SegmentCap)
            {
                var protectedSigs = new HashSet<string>(StringComparer.Ordinal);
                foreach (var sig in state.AnchoredPlayerComboSig.Values)
                    protectedSigs.Add(sig);
                foreach (var sig in state.StrengthPlayerComboSigs)
                    protectedSigs.Add(sig);
                var candidates = inSegment.Where(s => state.HistoryPool.TryGetValue(s, out var e) && e.Elo < elo && !protectedSigs.Contains(s)).ToList();
                if (candidates.Count == 0)
                    return;
                string? kickSig = null;
                double bestRedundancy = double.MinValue;
                double kickElo = double.MaxValue;
                foreach (var s in candidates)
                {
                    var redundancy = RedundancyInSegment(s, inSegment, state);
                    var candidateElo = state.HistoryPool[s].Elo;
                    if (redundancy > bestRedundancy || (redundancy == bestRedundancy && candidateElo < kickElo))
                    {
                        bestRedundancy = redundancy;
                        kickSig = s;
                        kickElo = candidateElo;
                    }
                }
                if (kickSig != null)
                    state.HistoryPool.TryRemove(kickSig, out _);
                else
                    return;
            }
            state.HistoryPool[comboSig] = new ComboEntry(comboSig, representative, elo, isLocalOptimum, isConfirmed, Math.Max(0, gamesPlayedDelta));
        }
    }

    private sealed class SilentBattleLogSink : IBattleLogSink
    {
        public void OnFrameStart(int timeMs, int frame) { }
        public void OnHpSnapshot(int timeMs, int side0Hp, int side1Hp) { }
        public void OnCast(BattleItemState caster, string itemName, int timeMs, int? ammoRemainingAfter = null) { }
        public void OnEffect(BattleItemState caster, string itemName, string effectKind, int value, int timeMs, bool isCrit = false, string? extraSuffix = null) { }
        public void OnBurnTick(BattleSide victim, int burnDamage, int remainingBurn, int timeMs) { }
        public void OnPoisonTick(BattleSide victim, int poisonDamage, int timeMs) { }
        public void OnRegenTick(BattleSide side, int heal, int timeMs) { }
        public void OnSandstormTick(int damage, int timeMs) { }
        public void OnResult(int winnerSideIndex, int timeMs, bool isDraw) { }
    }
}
