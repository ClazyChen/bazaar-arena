using BazaarArena.BattleSimulator;
using BazaarArena.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena.QualityDeckFinder;

/// <summary>单次爬山（组合空间）：组合邻域采样 + 首次改进 + MAB；对每个邻居先选代表排列再评估外战 ELO。</summary>
public static class HillClimb
{
    public delegate List<string> OpponentSelector(bool isNewDeck, double? deckElo, int m);

    public enum StopReason
    {
        Unknown = 0,
        NoNeighbors,
        NoImprovementInSample,
        ReachedMaxSteps,
    }

    public readonly record struct RunDiag(
        int StepsExecuted,
        int Iterations,
        int TotalNeighborsSampled,
        int TotalNeighborsEvaluated,
        int ZeroNeighborIterations,
        double BestDeltaEloSeen,
        StopReason StopReason,
        bool WhenLocalOpt_NextNotNull = false,
        double WhenLocalOpt_NextElo = 0,
        double WhenLocalOpt_CurrentElo = 0);

    /// <summary>
    /// 从起始组合（以代表排列表示）出发爬山，直到无改进或达到步数上限。
    /// 返回 (最终组合签名, 最终代表排列, 是否局部最优)。
    /// </summary>
    public static (string comboSig, DeckRep representative, bool isLocalOptimum) Run(
        string startComboSig,
        DeckRep startRepresentative,
        OptimizerState state,
        Config config,
        ItemPool pool,
        SimulatorClass simulator,
        IItemTemplateResolver db,
        Random? rng = null,
        string? anchorItemName = null,
        int? maxClimbStepsOverride = null,
        int? neighborSampleSizeOverride = null,
        int? mabBudgetPerStepOverride = null,
        OpponentSelector? opponentSelectorOverride = null)
    {
        var (sig, rep, isLocalOptimum, _) = RunWithDiag(
            startComboSig,
            startRepresentative,
            state,
            config,
            pool,
            simulator,
            db,
            rng,
            anchorItemName,
            maxClimbStepsOverride,
            neighborSampleSizeOverride,
            mabBudgetPerStepOverride,
            opponentSelectorOverride);
        return (sig, rep, isLocalOptimum);
    }

    /// <summary>
    /// 与 Run 相同，但额外返回诊断信息（用于排查“局部最优判定过宽/全员局部最优”等现象）。
    /// </summary>
    public static (string comboSig, DeckRep representative, bool isLocalOptimum, RunDiag diag) RunWithDiag(
        string startComboSig,
        DeckRep startRepresentative,
        OptimizerState state,
        Config config,
        ItemPool pool,
        SimulatorClass simulator,
        IItemTemplateResolver db,
        Random? rng = null,
        string? anchorItemName = null,
        int? maxClimbStepsOverride = null,
        int? neighborSampleSizeOverride = null,
        int? mabBudgetPerStepOverride = null,
        OpponentSelector? opponentSelectorOverride = null)
    {
        rng ??= Random.Shared;
        var opponentSelector = opponentSelectorOverride
            ?? ((isNewDeck, deckElo, m) => EloSystem.SelectOpponentSignatures(state, config, isNewDeck, deckElo, m, rng));

        var currentComboSig = startComboSig;
        var currentRep = startRepresentative;
        var currentElo = state.VirtualPlayerPool.TryGetValue(currentComboSig, out var e) ? e.Elo : config.InitialElo;
        int steps = 0;

        int maxSteps = maxClimbStepsOverride ?? config.MaxClimbSteps;
        int neighborSample = neighborSampleSizeOverride ?? config.NeighborSampleSize;
        int mabBudget = mabBudgetPerStepOverride ?? config.MabBudgetPerStep;

        int iterations = 0;
        int totalNeighborsSampled = 0;
        int totalNeighborsEvaluated = 0;
        int zeroNeighborIterations = 0;
        double bestDeltaSeen = double.NegativeInfinity;
        StopReason stopReason = StopReason.Unknown;
        int consecutiveNoImprove = 0;
        int minNoImproveRounds = Math.Max(1, config.MinNoImproveRoundsForLocalOptimum);

        while (steps < maxSteps)
        {
            iterations++;
            var neighbors = Neighborhood.SampleComboNeighborsWeighted(
                currentRep,
                pool,
                state.Priors,
                db,
                rng,
                neighborSample,
                config.ExploreMix,
                pairLambda: config.SynergyPairLambda,
                anchorItemName: anchorItemName,
                config: config,
                totalGames: state.TotalGames);
            totalNeighborsSampled += neighbors.Count;
            if (neighbors.Count == 0)
            {
                zeroNeighborIterations++;
                stopReason = StopReason.NoNeighbors;
                return (currentComboSig, currentRep, true,
                    new RunDiag(steps, iterations, totalNeighborsSampled, totalNeighborsEvaluated, zeroNeighborIterations, bestDeltaSeen, stopReason, false, 0, currentElo));
            }

            (string comboSig, DeckRep rep)? next = null;
            double nextElo = 0;

            if (neighbors.Count <= mabBudget * 2)
            {
                int evaluated;
                double bestDelta;
                var firstResult = FirstImprovement(currentComboSig, currentRep, currentElo, neighbors, state, config, pool, simulator, db, rng, opponentSelector, out evaluated, out bestDelta);
                totalNeighborsEvaluated += evaluated;
                bestDeltaSeen = Math.Max(bestDeltaSeen, bestDelta);
                if (firstResult != null)
                {
                    next = (firstResult.Value.comboSig, firstResult.Value.rep);
                    nextElo = firstResult.Value.elo;
                }
            }
            else
            {
                int evaluated;
                double bestDelta;
                (next, nextElo, evaluated, bestDelta) = MabFirstImprovement(currentComboSig, currentRep, currentElo, neighbors, state, config, pool, simulator, db, rng, mabBudget, opponentSelector);
                totalNeighborsEvaluated += evaluated;
                bestDeltaSeen = Math.Max(bestDeltaSeen, bestDelta);
            }

            if (next == null || nextElo <= currentElo)
            {
                consecutiveNoImprove++;
                if (consecutiveNoImprove < minNoImproveRounds)
                    continue;
                stopReason = StopReason.NoImprovementInSample;
                // 若本轮已前进过（steps>0），当前点已是改进结果，不应判为“局部最优”并触发重启，否则会丢弃改进。
                bool treatAsLocalOptimum = steps == 0;
                if (treatAsLocalOptimum)
                {
                    if (state.VirtualPlayerPool.TryGetValue(currentComboSig, out var ce))
                        state.VirtualPlayerPool[currentComboSig] = ce with { Representative = currentRep, Elo = currentElo, IsLocalOptimum = true };
                    else
                        state.VirtualPlayerPool[currentComboSig] = new ComboEntry(currentComboSig, currentRep, currentElo, true, false, 0);
                    EloSystem.TryAddToHistoryPool(state, config, currentComboSig, currentRep, currentElo, isLocalOptimum: true);
                }
                return (currentComboSig, currentRep, treatAsLocalOptimum,
                    new RunDiag(steps, iterations, totalNeighborsSampled, totalNeighborsEvaluated, zeroNeighborIterations, bestDeltaSeen, stopReason, next != null, nextElo, currentElo));
            }

            consecutiveNoImprove = 0;
            currentComboSig = next.Value.comboSig;
            currentRep = next.Value.rep;
            currentElo = nextElo;
            steps++;
        }

        stopReason = StopReason.ReachedMaxSteps;
        return (currentComboSig, currentRep, false,
            new RunDiag(steps, iterations, totalNeighborsSampled, totalNeighborsEvaluated, zeroNeighborIterations, bestDeltaSeen, stopReason, false, 0, 0));
    }

    /// <summary>首次改进：随机顺序评估组合邻居，按该顺序第一个严格更强则返回该组合、代表排列及用于判定的 ELO（与 Run 中 nextElo 同源，避免二次 TryGetEntry 导致误判）。</summary>
    private static (string comboSig, DeckRep rep, double elo)? FirstImprovement(
        string currentComboSig,
        DeckRep currentRep,
        double currentElo,
        List<(string comboSig, DeckRep seedRepresentative)> neighbors,
        OptimizerState state,
        Config config,
        ItemPool pool,
        SimulatorClass simulator,
        IItemTemplateResolver db,
        Random rng,
        OpponentSelector opponentSelector,
        out int evaluatedCount,
        out double bestDeltaElo)
    {
        evaluatedCount = 0;
        bestDeltaElo = double.NegativeInfinity;
        long tShuffle0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var ordered = neighbors.OrderBy(_ => rng.Next()).ToList();
        PerfCounters.AddHillNeighborShuffleTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tShuffle0);
        foreach (var n in ordered)
        {
            evaluatedCount++;
            var comboSig = n.comboSig;
            DeckRep rep;
            if (state.TryGetEntry(comboSig, out var ex))
            {
                rep = ex.Representative;
            }
            else
            {
                var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, n.seedRepresentative, state, config, pool, simulator, db, rng);
                rep = ensured.Representative;
            }

            var elo = state.TryGetEntry(comboSig, out var ee) ? ee.Elo : double.NaN;
            if (double.IsNaN(elo))
            {
                var opps = opponentSelector(isNewDeck: true, deckElo: null, m: Math.Max(1, config.GamesPerEval));
                elo = EloSystem.RunGamesAndUpdateElo(comboSig, rep, opps, state, config, simulator, db);
            }
            bestDeltaElo = Math.Max(bestDeltaElo, elo - currentElo);
            if (elo > currentElo)
                return (comboSig, rep, elo);
        }
        return null;
    }

    /// <summary>MAB 选择下一个要评估的组合邻居；找到严格更强则返回，否则预算用完后返回 null。</summary>
    private static ((string comboSig, DeckRep rep)? next, double nextElo, int evaluatedCount, double bestDeltaElo) MabFirstImprovement(
        string currentComboSig,
        DeckRep currentRep,
        double currentElo,
        List<(string comboSig, DeckRep seedRepresentative)> neighbors,
        OptimizerState state,
        Config config,
        ItemPool pool,
        SimulatorClass simulator,
        IItemTemplateResolver db,
        Random rng,
        int mabBudgetPerStep,
        OpponentSelector opponentSelector)
    {
        long tMab0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var n = neighbors.Count;
        var lastElo = new double[n];
        var count = new int[n];
        for (int i = 0; i < n; i++)
            lastElo[i] = config.InitialElo;
        var C = 2.0;
        int totalEvals = 0;
        int evaluatedCount = 0;
        double bestDeltaElo = double.NegativeInfinity;

        for (int b = 0; b < mabBudgetPerStep; b++)
        {
            int bestIdx = -1;
            double bestUcb = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                double ucb = lastElo[i] + C * Math.Sqrt(Math.Log(1 + totalEvals) / (1 + count[i]));
                if (ucb > bestUcb) { bestUcb = ucb; bestIdx = i; }
            }
            if (bestIdx < 0) break;

            var neighbor = neighbors[bestIdx];
            var comboSig = neighbor.comboSig;
            DeckRep rep;
            if (state.TryGetEntry(comboSig, out var ex))
            {
                rep = ex.Representative;
            }
            else
            {
                var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, neighbor.seedRepresentative, state, config, pool, simulator, db, rng);
                rep = ensured.Representative;
            }
            var opps = opponentSelector(isNewDeck: false, deckElo: currentElo, m: Math.Max(1, config.GamesPerEval));
            var elo = EloSystem.RunGamesAndUpdateElo(comboSig, rep, opps, state, config, simulator, db);
            lastElo[bestIdx] = elo;
            count[bestIdx]++;
            totalEvals++;
            evaluatedCount++;
            bestDeltaElo = Math.Max(bestDeltaElo, elo - currentElo);

            if (elo > currentElo) return ((comboSig, rep), elo, evaluatedCount, bestDeltaElo);
        }

        for (int i = 0; i < n; i++)
        {
            if (count[i] > 0 && lastElo[i] > currentElo)
            {
                var comboSig = neighbors[i].comboSig;
                var rep = state.TryGetEntry(comboSig, out var ex) ? ex.Representative : neighbors[i].seedRepresentative;
                // 使用评估得到的 lastElo[i] 作为 nextElo，与 FirstImprovement 一致，避免二次 TryGetEntry 导致判定不一致
                PerfCounters.AddHillMabTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tMab0);
                return ((comboSig, rep), lastElo[i], evaluatedCount, bestDeltaElo);
            }
        }
        PerfCounters.AddHillMabTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tMab0);
        return (null, 0, evaluatedCount, bestDeltaElo);
    }
}
