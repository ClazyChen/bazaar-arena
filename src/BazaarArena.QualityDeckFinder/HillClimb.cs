using BazaarArena.BattleSimulator;
using BazaarArena.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena.QualityDeckFinder;

/// <summary>单次爬山（组合空间）：组合邻域采样 + 首次改进 + MAB；对每个邻居先选代表排列再评估外战 ELO。</summary>
public static class HillClimb
{
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
        Random? rng = null)
    {
        rng ??= Random.Shared;
        var currentComboSig = startComboSig;
        var currentRep = startRepresentative;
        var currentElo = state.Pool.TryGetValue(currentComboSig, out var e) ? e.Elo : config.InitialElo;
        int steps = 0;

        while (steps < config.MaxClimbSteps)
        {
            var neighbors = Neighborhood.SampleComboNeighborsWeighted(
                currentRep,
                pool,
                state.Priors,
                rng,
                config.NeighborSampleSize,
                config.ExploreMix);
            if (neighbors.Count == 0) return (currentComboSig, currentRep, true);

            (string comboSig, DeckRep rep)? next = null;
            double nextElo = 0;

            if (neighbors.Count <= config.MabBudgetPerStep * 2)
            {
                next = FirstImprovement(currentComboSig, currentRep, currentElo, neighbors, state, config, pool, simulator, db, rng);
                if (next != null)
                    nextElo = state.Pool.TryGetValue(next.Value.comboSig, out var ne) ? ne.Elo : config.InitialElo;
            }
            else
            {
                (next, nextElo) = MabFirstImprovement(currentComboSig, currentRep, currentElo, neighbors, state, config, pool, simulator, db, rng);
            }

            if (next == null || nextElo <= currentElo)
            {
                if (state.Pool.TryGetValue(currentComboSig, out var ce))
                    state.Pool[currentComboSig] = ce with { Representative = currentRep, Elo = currentElo, IsLocalOptimum = true };
                else
                    EloSystem.TryAddToPool(state, config, currentComboSig, currentRep, currentElo, isLocalOptimum: true);
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
        Random rng)
    {
        var ordered = neighbors.OrderBy(_ => rng.Next()).ToList();
        foreach (var n in ordered)
        {
            var comboSig = n.comboSig;
            DeckRep rep;
            if (state.Pool.TryGetValue(comboSig, out var ex))
            {
                rep = ex.Representative;
            }
            else
            {
                var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, n.seedRepresentative, state, config, pool, simulator, db, rng);
                rep = ensured.Representative;
            }

            var elo = state.Pool.TryGetValue(comboSig, out var ee) ? ee.Elo : double.NaN;
            if (double.IsNaN(elo))
            {
                var opps = EloSystem.SelectOpponentSignatures(state, config, isNewDeck: true, null, Math.Max(1, config.GamesPerEval));
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
        Random rng)
    {
        var n = neighbors.Count;
        var lastElo = new double[n];
        var count = new int[n];
        for (int i = 0; i < n; i++)
            lastElo[i] = config.InitialElo;
        var C = 2.0;
        int totalEvals = 0;

        for (int b = 0; b < config.MabBudgetPerStep; b++)
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
            if (state.Pool.TryGetValue(comboSig, out var ex))
            {
                rep = ex.Representative;
            }
            else
            {
                var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, neighbor.seedRepresentative, state, config, pool, simulator, db, rng);
                rep = ensured.Representative;
            }
            var opps = EloSystem.SelectOpponentSignatures(state, config, false, currentElo, Math.Max(1, config.GamesPerEval));
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
                var rep = state.Pool.TryGetValue(comboSig, out var ex) ? ex.Representative : neighbors[i].seedRepresentative;
                var finalElo = state.Pool.TryGetValue(comboSig, out var ex2) ? ex2.Elo : lastElo[i];
                return ((comboSig, rep), finalElo);
            }
        }
        return (null, 0);
    }
}
