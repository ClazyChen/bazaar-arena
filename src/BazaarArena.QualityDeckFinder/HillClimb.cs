using BazaarArena.BattleSimulator;
using BazaarArena.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena.QualityDeckFinder;

/// <summary>单次爬山：邻域采样 + 首次改进 + MAB；返回最终卡组及是否局部最优。</summary>
public static class HillClimb
{
    /// <summary>从当前卡组出发爬山，直到无改进或达到步数上限。返回 (最终卡组, 是否局部最优)。</summary>
    public static (DeckRep deck, bool isLocalOptimum) Run(
        DeckRep start,
        OptimizerState state,
        Config config,
        ItemPool pool,
        SimulatorClass simulator,
        IItemTemplateResolver db)
    {
        var current = start;
        var currentElo = state.Pool.TryGetValue(current.Signature(), out var e) ? e.Elo : config.InitialElo;
        int steps = 0;

        while (steps < config.MaxClimbSteps)
        {
            var neighbors = Neighborhood.SampleNeighbors(current, pool, Random.Shared, config.NeighborSampleSize);
            if (neighbors.Count == 0) return (current, true);

            DeckRep? next = null;
            double nextElo = 0;

            if (neighbors.Count <= config.MabBudgetPerStep * 2)
            {
                next = FirstImprovement(current, currentElo, neighbors, state, config, simulator, db);
                if (next != null)
                    nextElo = state.Pool.TryGetValue(next.Signature(), out var ne) ? ne.Elo : config.InitialElo;
            }
            else
            {
                (next, nextElo) = MabFirstImprovement(current, currentElo, neighbors, state, config, simulator, db);
            }

            if (next == null || nextElo <= currentElo)
            {
                state.Pool[current.Signature()] = new DeckEntry(current, currentElo, true, state.Pool.TryGetValue(current.Signature(), out var ce) ? ce.GameCount : 0);
                return (current, true);
            }

            current = next;
            currentElo = nextElo;
            steps++;
        }

        return (current, false);
    }

    /// <summary>首次改进：随机顺序评估邻居，第一个严格更强则返回该邻居。</summary>
    private static DeckRep? FirstImprovement(DeckRep current, double currentElo, List<DeckRep> neighbors, OptimizerState state, Config config, SimulatorClass simulator, IItemTemplateResolver db)
    {
        var order = neighbors.OrderBy(_ => Random.Shared.Next()).ToList();
        foreach (var n in order)
        {
            var sig = n.Signature();
            var elo = state.Pool.TryGetValue(sig, out var ex) ? ex.Elo : double.NaN;
            if (double.IsNaN(elo))
            {
                var opps = EloSystem.SelectOpponentSignatures(state, config, false, currentElo, Math.Max(1, config.GamesPerEval));
                elo = EloSystem.RunGamesAndUpdateElo(n, opps, state, config, simulator, db);
            }
            if (elo > currentElo) return n;
        }
        return null;
    }

    /// <summary>MAB 选择下一个要评估的邻居；找到严格更强则返回，否则预算用完后返回 null。</summary>
    private static (DeckRep? next, double nextElo) MabFirstImprovement(DeckRep current, double currentElo, List<DeckRep> neighbors, OptimizerState state, Config config, SimulatorClass simulator, IItemTemplateResolver db)
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
            var opps = EloSystem.SelectOpponentSignatures(state, config, false, currentElo, Math.Max(1, config.GamesPerEval));
            var elo = EloSystem.RunGamesAndUpdateElo(neighbor, opps, state, config, simulator, db);
            lastElo[bestIdx] = elo;
            count[bestIdx]++;
            totalEvals++;

            if (elo > currentElo) return (neighbor, elo);
        }

        for (int i = 0; i < n; i++)
        {
            if (count[i] > 0 && lastElo[i] > currentElo)
            {
                var neighbor = neighbors[i];
                var sig = neighbor.Signature();
                var finalElo = state.Pool.TryGetValue(sig, out var ex) ? ex.Elo : lastElo[i];
                return (neighbor, finalElo);
            }
        }
        return (null, 0);
    }
}
