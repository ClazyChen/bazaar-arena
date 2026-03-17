using BazaarArena.BattleSimulator;
using BazaarArena.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena.QualityDeckFinder;

/// <summary>主循环：随机重启 + 局部爬山；周期性 Top10 输出与状态保存。支持 --workers N 多 worker 并行。</summary>
public static class Runner
{
    public static void Run(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config)
    {
        var rng = state.RngSeed.HasValue ? new Random(state.RngSeed.Value) : new Random();

        if (state.Pool.Count == 0)
            SeedPool(simulator, db, pool, state, config, rng);

        if (config.Workers > 0)
        {
            RunWithWorkers(simulator, db, pool, state, config, rng);
            return;
        }

        int climbsSinceTop = 0;
        int climbsSinceSave = 0;
        int climbsSinceInject = 0;

        while (true)
        {
            DoOneClimb(simulator, db, pool, state, config, rng);
            climbsSinceTop++;
            climbsSinceSave++;
            climbsSinceInject++;

            if (climbsSinceTop >= config.TopInterval)
            {
                climbsSinceTop = 0;
                MaybeExpandSegmentBounds(state, config);
                Top10Report.Print(state);
            }

            if (climbsSinceSave >= config.SaveInterval)
            {
                climbsSinceSave = 0;
                MaybeExpandSegmentBounds(state, config);
                StatePersistence.Save(config.StatePath, state);
                Console.WriteLine($"[已保存状态到 {config.StatePath}]");
            }

            if (config.InjectInterval > 0 && climbsSinceInject >= config.InjectInterval)
            {
                climbsSinceInject = 0;
                InjectRandomDecks(simulator, db, pool, state, config, rng);
            }
        }
    }

    /// <summary>多 worker 模式：N 个 worker 各自循环执行「重启+爬山+入池」，主线程仅做周期报告/保存/注入。</summary>
    private static void RunWithWorkers(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config, Random mainRng)
    {
        var reportEvent = new ManualResetEvent(false);
        var reportLock = new object();
        int climbsSinceTop = 0;
        int climbsSinceSave = 0;
        int climbsSinceInject = 0;
        var baseSeed = state.RngSeed ?? Environment.TickCount;

        for (int w = 0; w < config.Workers; w++)
        {
            var workerId = w;
            var workerRng = new Random(baseSeed + workerId);
            var t = new Thread(() =>
            {
                while (true)
                {
                    if (DoOneClimb(simulator, db, pool, state, config, workerRng))
                    {
                        Interlocked.Increment(ref climbsSinceTop);
                        Interlocked.Increment(ref climbsSinceSave);
                        Interlocked.Increment(ref climbsSinceInject);
                        if (Volatile.Read(ref climbsSinceTop) >= config.TopInterval)
                            reportEvent.Set();
                    }
                }
            })
            { IsBackground = true };
            t.Start();
        }

        while (true)
        {
            reportEvent.WaitOne();
            lock (reportLock)
            {
                var top = Interlocked.Exchange(ref climbsSinceTop, 0);
                if (top <= 0) { reportEvent.Reset(); continue; }
                MaybeExpandSegmentBounds(state, config);
                Top10Report.Print(state);
                var save = Volatile.Read(ref climbsSinceSave);
                if (save >= config.SaveInterval)
                {
                    StatePersistence.Save(config.StatePath, state);
                    Console.WriteLine($"[已保存状态到 {config.StatePath}]");
                    Interlocked.Add(ref climbsSinceSave, -config.SaveInterval);
                }
                var inj = Volatile.Read(ref climbsSinceInject);
                if (config.InjectInterval > 0 && inj >= config.InjectInterval)
                {
                    InjectRandomDecks(simulator, db, pool, state, config, mainRng);
                    Interlocked.Add(ref climbsSinceInject, -config.InjectInterval);
                }
            }
            reportEvent.Reset();
        }
    }

    /// <summary>执行一次「随机起点 + 新卡组评估 + 爬山 + 入池」。返回是否完成了一次爬山。</summary>
    private static bool DoOneClimb(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config, Random rng)
    {
        var shapeIndex = PickShapeIndex(state, config, rng);
        var shape = Shapes.GetRandomPermutation(shapeIndex, rng);
        if (!pool.CanBuildNoDuplicate(shape)) return false;

        var startSeed = DeckGen.RandomDeckWeighted(shape, pool, state.Priors, rng, config.ExploreMix);
        if (startSeed == null) return false;

        state.IncrementTotalRestarts();
        var comboSig = ComboSignature.FromDeckRep(startSeed);
        var isNew = !state.Pool.ContainsKey(comboSig);

        // 组合内部先选代表排列（内战+外战确认的外战阶段会在后面做；这里先拿到较好的代表用于初始 Elo 评估）
        var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, startSeed, state, config, pool, simulator, db, rng);
        var startRep = ensured.Representative;

        if (isNew)
        {
            var opps = EloSystem.SelectOpponentSignatures(state, config, isNewDeck: true, null, Math.Max(1, config.GamesPerEval * 2));
            if (opps.Count == 0)
                EloSystem.TryAddToPool(state, config, comboSig, startRep, config.InitialElo);
            else
                EloSystem.RunGamesAndUpdateElo(comboSig, startRep, opps, state, config, simulator, db);
        }

        if (state.Pool.TryGetValue(comboSig, out var startEntry))
            state.Priors.ObserveCombo(startEntry.Representative, startEntry.Elo, config, db);

        var (finalComboSig, finalRep, _) = HillClimb.Run(comboSig, startRep, state, config, pool, simulator, db, rng);
        state.IncrementTotalClimbs();
        var finalElo = state.Pool.TryGetValue(finalComboSig, out var fe) ? fe.Elo : config.InitialElo;
        EloSystem.TryAddToPool(state, config, finalComboSig, finalRep, finalElo, isLocalOptimum: false);
        state.Priors.ObserveCombo(finalRep, finalElo, config, db);
        return true;
    }

    /// <summary>当池内最高 ELO 超过当前最高段下界一定幅度时，向高分方向追加分段边界。</summary>
    private static void MaybeExpandSegmentBounds(OptimizerState state, Config config)
    {
        lock (config.SegmentBoundsLock)
        {
            var bounds = config.SegmentBounds;
            if (bounds.Count == 0 || bounds.Count >= config.SegmentExpandMaxBounds)
                return;
            if (state.Pool.Count == 0) return;

            double maxElo = state.Pool.Values.Max(e => e.Elo);
            double step = config.SegmentExpandStep;
            while (bounds.Count < config.SegmentExpandMaxBounds && maxElo > bounds[^1] + step)
            {
                bounds.Add(bounds[^1] + step);
            }
        }
    }

    /// <summary>注入若干随机新卡组（与重启同分布），打初始对局后入池，不以其为起点爬山。</summary>
    private static void InjectRandomDecks(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config, Random rng)
    {
        int added = 0;
        int tries = Math.Max(config.InjectCount * 3, 10);
        for (int t = 0; t < tries && added < config.InjectCount; t++)
        {
            var shapeIndex = PickShapeIndex(state, config, rng);
            var shape = Shapes.GetRandomPermutation(shapeIndex, rng);
            if (!pool.CanBuildNoDuplicate(shape)) continue;

            var deck = DeckGen.RandomDeckWeighted(shape, pool, state.Priors, rng, config.ExploreMix);
            if (deck == null) continue;

            var comboSig = ComboSignature.FromDeckRep(deck);
            if (state.Pool.ContainsKey(comboSig)) continue;

            var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, deck, state, config, pool, simulator, db, rng);
            var rep = ensured.Representative;

            var opps = EloSystem.SelectOpponentSignatures(state, config, isNewDeck: true, null, Math.Max(1, config.GamesPerEval * 2));
            if (opps.Count == 0)
                EloSystem.TryAddToPool(state, config, comboSig, rep, config.InitialElo);
            else
                EloSystem.RunGamesAndUpdateElo(comboSig, rep, opps, state, config, simulator, db);
            if (state.Pool.TryGetValue(comboSig, out var entry))
                state.Priors.ObserveCombo(entry.Representative, entry.Elo, config, db);
            added++;
        }
    }

    private static void SeedPool(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config, Random rng)
    {
        for (int shapeIndex = 0; shapeIndex < Shapes.All.Count; shapeIndex++)
        {
            for (int k = 0; k < 3; k++)
            {
                var shape = Shapes.GetRandomPermutation(shapeIndex, rng);
                if (!pool.CanBuildNoDuplicate(shape)) continue;
                var deck = DeckGen.RandomDeckWeighted(shape, pool, state.Priors, rng, config.ExploreMix);
                if (deck == null) continue;
                var comboSig = ComboSignature.FromDeckRep(deck);
                if (state.Pool.ContainsKey(comboSig)) continue;
                var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, deck, state, config, pool, simulator, db, rng);
                EloSystem.TryAddToPool(state, config, comboSig, ensured.Representative, config.InitialElo);
            }
        }

        foreach (var kv in state.Pool.ToList())
        {
            var opps = state.Pool.Keys.Where(s => s != kv.Key).Take(Math.Max(1, config.GamesPerEval)).ToList();
            if (opps.Count == 0) continue;
            EloSystem.RunGamesAndUpdateElo(kv.Key, kv.Value.Representative, opps, state, config, simulator, db);
        }
    }

    private static int PickShapeIndex(OptimizerState state, Config config, Random rng)
    {
        if (Shapes.All.Count <= 1) return 0;
        if (rng.NextDouble() < Math.Clamp(config.ExploreMix, 0.0, 1.0))
            return rng.Next(Shapes.All.Count);

        var weights = new List<double>(Shapes.All.Count);
        for (int i = 0; i < Shapes.All.Count; i++)
        {
            var counts = ComboSignature.ShapeCounts(Shapes.All[i]);
            weights.Add(state.Priors.ShapeWeight(counts));
        }
        return WeightedPick.PickIndex(weights, rng);
    }
}
