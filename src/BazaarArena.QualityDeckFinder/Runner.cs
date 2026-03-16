using BazaarArena.BattleSimulator;
using BazaarArena.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena.QualityDeckFinder;

/// <summary>主循环：随机重启 + 局部爬山；周期性 Top10 输出与状态保存。</summary>
public static class Runner
{
    public static void Run(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config)
    {
        var rng = state.RngSeed.HasValue ? new Random(state.RngSeed.Value) : new Random();

        if (state.Pool.Count == 0)
            SeedPool(simulator, db, pool, state, config, rng);

        int climbsSinceTop = 0;
        int climbsSinceSave = 0;

        while (true)
        {
            var shapeIndex = rng.Next(Shapes.All.Count);
            var shape = Shapes.ByIndex(shapeIndex);
            if (!pool.CanBuildNoDuplicate(shape)) continue;

            var start = DeckGen.RandomDeck(shape, pool, rng);
            if (start == null) continue;

            state.TotalRestarts++;
            var sig = start.Signature();
            var isNew = !state.Pool.ContainsKey(sig);

            if (isNew)
            {
                var opps = EloSystem.SelectOpponentSignatures(state, config, isNewDeck: true, null, Math.Max(1, config.GamesPerEval * 2));
                if (opps.Count == 0)
                {
                    EloSystem.TryAddToPool(state, config, start, config.InitialElo);
                }
                else
                {
                    EloSystem.RunGamesAndUpdateElo(start, opps, state, config, simulator, db);
                }
            }

            var (final, isLocalOptimum) = HillClimb.Run(start, state, config, pool, simulator, db);
            state.TotalClimbs++;
            climbsSinceTop++;
            climbsSinceSave++;

            var finalElo = state.Pool.TryGetValue(final.Signature(), out var fe) ? fe.Elo : config.InitialElo;
            EloSystem.TryAddToPool(state, config, final, finalElo, isLocalOptimum);

            if (climbsSinceTop >= config.TopInterval)
            {
                climbsSinceTop = 0;
                Top10Report.Print(state);
            }

            if (climbsSinceSave >= config.SaveInterval)
            {
                climbsSinceSave = 0;
                StatePersistence.Save(config.StatePath, state);
                Console.WriteLine($"[已保存状态到 {config.StatePath}]");
            }
        }
    }

    private static void SeedPool(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config, Random rng)
    {
        foreach (var shape in Shapes.All)
        {
            if (!pool.CanBuildNoDuplicate(shape)) continue;
            for (int k = 0; k < 3; k++)
            {
                var deck = DeckGen.RandomDeck(shape, pool, rng);
                if (deck == null) continue;
                var sig = deck.Signature();
                if (state.Pool.ContainsKey(sig)) continue;
                EloSystem.TryAddToPool(state, config, deck, config.InitialElo);
            }
        }

        foreach (var kv in state.Pool.ToList())
        {
            var opps = state.Pool.Keys.Where(s => s != kv.Key).Take(Math.Max(1, config.GamesPerEval)).ToList();
            if (opps.Count == 0) continue;
            EloSystem.RunGamesAndUpdateElo(kv.Value.Deck, opps, state, config, simulator, db);
        }
    }
}
