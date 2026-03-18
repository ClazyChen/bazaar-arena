using BazaarArena.BattleSimulator;
using BazaarArena.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena.QualityDeckFinder;

/// <summary>单次爬山（组合空间）：组合邻域采样 + 首次改进 + MAB；对每个邻居先选代表排列再评估外战 ELO。</summary>
public static class HillClimb
{
    public delegate List<string> OpponentSelector(bool isNewDeck, double? deckElo, int m);

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

        while (steps < maxSteps)
        {
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
            if (neighbors.Count == 0) return (currentComboSig, currentRep, true);

            (string comboSig, DeckRep rep)? next = null;
            double nextElo = 0;

            if (neighbors.Count <= mabBudget * 2)
            {
                next = FirstImprovement(currentComboSig, currentRep, currentElo, neighbors, state, config, pool, simulator, db, rng, opponentSelector);
                if (next != null)
                    nextElo = state.TryGetEntry(next.Value.comboSig, out var ne) ? ne.Elo : config.InitialElo;
            }
            else
            {
                (next, nextElo) = MabFirstImprovement(currentComboSig, currentRep, currentElo, neighbors, state, config, pool, simulator, db, rng, mabBudget, opponentSelector);
            }

            if (next == null || nextElo <= currentElo)
            {
                if (state.VirtualPlayerPool.TryGetValue(currentComboSig, out var ce))
                    state.VirtualPlayerPool[currentComboSig] = ce with { Representative = currentRep, Elo = currentElo, IsLocalOptimum = true };
                else
                    state.VirtualPlayerPool[currentComboSig] = new ComboEntry(currentComboSig, currentRep, currentElo, true, false, 0);

                EloSystem.TryAddToHistoryPool(state, config, currentComboSig, currentRep, currentElo, isLocalOptimum: true);
                return (currentComboSig, currentRep, true);
            }

            currentComboSig = next.Value.comboSig;
            currentRep = next.Value.rep;
            currentElo = nextElo;
            steps++;
        }

        return (currentComboSig, currentRep, false);
    }

    /// <summary>首次改进：随机顺序评估组合邻居，按该顺序第一个严格更强则返回该组合及其代表排列。</summary>
    private static (string comboSig, DeckRep rep)? FirstImprovement(
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
        OpponentSelector opponentSelector)
    {
        var ordered = neighbors.OrderBy(_ => rng.Next()).ToList();
        foreach (var n in ordered)
        {
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
            if (elo > currentElo)
                return (comboSig, rep);
        }
        return null;
    }

    /// <summary>MAB 选择下一个要评估的组合邻居；找到严格更强则返回，否则预算用完后返回 null。</summary>
    private static ((string comboSig, DeckRep rep)? next, double nextElo) MabFirstImprovement(
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
        var n = neighbors.Count;
        var lastElo = new double[n];
        var count = new int[n];
        for (int i = 0; i < n; i++)
            lastElo[i] = config.InitialElo;
        var C = 2.0;
        int totalEvals = 0;

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

            if (elo > currentElo) return ((comboSig, rep), elo);
        }

        for (int i = 0; i < n; i++)
        {
            if (count[i] > 0 && lastElo[i] > currentElo)
            {
                var comboSig = neighbors[i].comboSig;
                var rep = state.TryGetEntry(comboSig, out var ex) ? ex.Representative : neighbors[i].seedRepresentative;
                var finalElo = state.TryGetEntry(comboSig, out var ex2) ? ex2.Elo : lastElo[i];
                return ((comboSig, rep), finalElo);
            }
        }
        return (null, 0);
    }
}
