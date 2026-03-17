using BazaarArena.BattleSimulator;
using BazaarArena.Core;
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
        int climbsSinceRerate = 0;

        while (true)
        {
            DoOneClimb(simulator, db, pool, state, config, rng);
            climbsSinceTop++;
            climbsSinceSave++;
            climbsSinceInject++;
            climbsSinceRerate++;

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

            if (config.RerateIntervalClimbs > 0 && climbsSinceRerate >= config.RerateIntervalClimbs)
            {
                climbsSinceRerate = 0;
                ReratePool(simulator, db, pool, state, config, rng);
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
        int climbsSinceRerate = 0;
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
                        Interlocked.Increment(ref climbsSinceRerate);
                        if (Volatile.Read(ref climbsSinceTop) >= config.TopInterval)
                            reportEvent.Set();
                        if (config.RerateIntervalClimbs > 0 && Volatile.Read(ref climbsSinceRerate) >= config.RerateIntervalClimbs)
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

                var rr = Volatile.Read(ref climbsSinceRerate);
                if (config.RerateIntervalClimbs > 0 && rr >= config.RerateIntervalClimbs)
                {
                    ReratePool(simulator, db, pool, state, config, mainRng);
                    Interlocked.Add(ref climbsSinceRerate, -config.RerateIntervalClimbs);
                }
            }
            reportEvent.Reset();
        }
    }

    /// <summary>执行一次「随机起点 + 新卡组评估 + 爬山 + 入池」。返回是否完成了一次爬山。</summary>
    private static bool DoOneClimb(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config, Random rng)
    {
        // anchored：固定某件物品做“最优拍档”搜索
        string? anchorItemName = null;
        bool anchored = rng.NextDouble() < Math.Clamp(config.AnchoredMix, 0.0, 1.0);
        int shapeIndex;
        if (anchored)
        {
            anchorItemName = PickAnchorItemName(db, pool, state);
            if (anchorItemName == null)
                anchored = false;
        }

        if (anchored && anchorItemName != null)
        {
            shapeIndex = PickShapeIndexForAnchor(db, state, config, anchorItemName, rng);
        }
        else
        {
            shapeIndex = PickShapeIndex(state, config, rng);
        }

        var shape = Shapes.GetRandomPermutation(shapeIndex, rng);
        if (!pool.CanBuildNoDuplicate(shape)) return false;

        var startSeed = anchored && anchorItemName != null
            ? DeckGen.RandomDeckWeightedSynergyAnchored(
                shape,
                pool,
                state.Priors,
                db,
                rng,
                config.ExploreMix,
                config.SynergyPairLambda,
                config.SynergyMechanicLambda,
                anchorItemName,
                config,
                state.TotalGames)
            : DeckGen.RandomDeckWeightedSynergy(
                shape,
                pool,
                state.Priors,
                db,
                rng,
                config.ExploreMix,
                config.SynergyPairLambda,
                config.SynergyMechanicLambda,
                config,
                state.TotalGames);
        if (startSeed == null) return false;

        state.IncrementTotalRestarts();
        var comboSig = ComboSignature.FromDeckRep(startSeed);
        var isNew = !state.Pool.ContainsKey(comboSig);

        // 组合内部先选代表排列（内战+外战确认的外战阶段会在后面做；这里先拿到较好的代表用于初始 Elo 评估）
        var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, startSeed, state, config, pool, simulator, db, rng);
        var startRep = ensured.Representative;
        var startIsConfirmed = ensured.IsConfirmed;

        if (isNew)
        {
            var opps = EloSystem.SelectOpponentSignatures(state, config, isNewDeck: true, null, Math.Max(1, config.GamesPerEval * 2));
            if (opps.Count == 0)
                EloSystem.TryAddToPool(state, config, comboSig, startRep, config.InitialElo, isConfirmed: startIsConfirmed);
            else
                EloSystem.RunGamesAndUpdateElo(comboSig, startRep, opps, state, config, simulator, db);
        }
        else
        {
            // 已存在组合也可能在本次 EnsureRepresentative 中完成了外战确认；将确认标记写回池。
            if (startIsConfirmed && state.Pool.TryGetValue(comboSig, out var ex))
                EloSystem.TryAddToPool(state, config, comboSig, startRep, ex.Elo, isConfirmed: true);
        }

        if (state.Pool.TryGetValue(comboSig, out var startEntry))
            state.Priors.ObserveCombo(
                startEntry.Representative,
                startEntry.Elo,
                config,
                db,
                isConfirmed: startEntry.IsConfirmed,
                gameCount: startEntry.GameCount);

        // fast lane：新卡组若初评 Elo 跳涨显著，则先孵化，再按段内胜率决定是否冲刺
        string currentSigForClimb = comboSig;
        var currentRepForClimb = startRep;

        double currentEloForClimb = state.Pool.TryGetValue(currentSigForClimb, out var ce0) ? ce0.Elo : config.InitialElo;
        var eloDelta = currentEloForClimb - config.InitialElo;
        bool useFastLane = config.FastLaneEnabled && isNew && eloDelta >= config.FastLaneEloDeltaThreshold;

        if (useFastLane)
        {
            var stats = state.StatsByComboSig.GetOrAdd(currentSigForClimb, _ => new OptimizerState.ComboStats());
            stats.Stage = OptimizerState.FastLaneStage.Incubate;
            Console.WriteLine($"【FastLane】触发孵化 Δ={eloDelta:0.0} sig={currentSigForClimb}");

            var (incSig, incRep, _) = HillClimb.Run(
                currentSigForClimb,
                currentRepForClimb,
                state,
                config,
                pool,
                simulator,
                db,
                rng,
                anchorItemName: anchorItemName,
                maxClimbStepsOverride: config.FastLaneIncubateMaxClimbSteps,
                neighborSampleSizeOverride: config.FastLaneIncubateNeighborSampleSize,
                mabBudgetPerStepOverride: config.FastLaneIncubateMabBudgetPerStep);
            currentSigForClimb = incSig;
            currentRepForClimb = incRep;
            currentEloForClimb = state.Pool.TryGetValue(currentSigForClimb, out var ce1) ? ce1.Elo : config.InitialElo;

            var (wr, games) = state.RecentWinRateInSegAndPrev(currentSigForClimb, currentEloForClimb);
            if (games >= Math.Max(1, config.FastLaneWinrateWindowGames) && wr >= config.FastLaneWinrateThreshold)
            {
                Console.WriteLine($"【FastLane】进入冲刺 winrate={wr:0.000} games={games} sig={currentSigForClimb}");
                var stats2 = state.StatsByComboSig.GetOrAdd(currentSigForClimb, _ => new OptimizerState.ComboStats());
                stats2.Stage = OptimizerState.FastLaneStage.Sprint;

                HillClimb.OpponentSelector sprintSelector = (isNewDeck2, deckElo2, m) =>
                {
                    if (isNewDeck2) return EloSystem.SelectOpponentSignatures(state, config, isNewDeck: true, null, m, rng);
                    return EloSystem.SelectOpponentSignaturesForSprint(state, config, deckElo2 ?? currentEloForClimb, m, rng);
                };

                var (spSig, spRep, _) = HillClimb.Run(
                    currentSigForClimb,
                    currentRepForClimb,
                    state,
                    config,
                    pool,
                    simulator,
                    db,
                    rng,
                    anchorItemName: anchorItemName,
                    maxClimbStepsOverride: config.FastLaneSprintMaxClimbSteps,
                    neighborSampleSizeOverride: config.FastLaneSprintNeighborSampleSize,
                    mabBudgetPerStepOverride: config.FastLaneSprintMabBudgetPerStep,
                    opponentSelectorOverride: sprintSelector);

                currentSigForClimb = spSig;
                currentRepForClimb = spRep;
                currentEloForClimb = state.Pool.TryGetValue(currentSigForClimb, out var ce2) ? ce2.Elo : config.InitialElo;

                var (wr2, games2) = state.RecentWinRateInSegAndPrev(currentSigForClimb, currentEloForClimb);
                if (games2 >= Math.Max(1, config.FastLaneWinrateWindowGames) && wr2 < config.FastLaneSprintFallbackThreshold)
                {
                    Console.WriteLine($"【FastLane】冲刺回退 winrate={wr2:0.000} games={games2} sig={currentSigForClimb}");
                    var stats3 = state.StatsByComboSig.GetOrAdd(currentSigForClimb, _ => new OptimizerState.ComboStats());
                    stats3.Stage = OptimizerState.FastLaneStage.Incubate;
                }
            }
        }

        var (finalComboSig, finalRep, isLocalOptimum) = useFastLane
            ? (currentSigForClimb, currentRepForClimb, false)
            : HillClimb.Run(comboSig, startRep, state, config, pool, simulator, db, rng, anchorItemName: anchorItemName);

        state.IncrementTotalClimbs();
        var finalElo = state.Pool.TryGetValue(finalComboSig, out var fe) ? fe.Elo : config.InitialElo;
        EloSystem.TryAddToPool(state, config, finalComboSig, finalRep, finalElo, isLocalOptimum: isLocalOptimum);
        var entryForLearn = state.Pool.TryGetValue(finalComboSig, out var ee) ? ee : null;
        state.Priors.ObserveCombo(
            finalRep,
            finalElo,
            config,
            db,
            isConfirmed: entryForLearn?.IsConfirmed ?? false,
            gameCount: entryForLearn?.GameCount ?? 0);
        return true;
    }

    private static string? PickAnchorItemName(IItemTemplateResolver db, ItemPool pool, OptimizerState state)
    {
        var all = pool.SmallNames.Concat(pool.MediumNames).Concat(pool.LargeNames).ToList();
        if (all.Count == 0) return null;
        var idx = state.NextAnchorPickIndex();
        var name = all[Math.Abs(idx) % all.Count];
        return db.GetTemplate(name) != null ? name : null;
    }

    private static int PickShapeIndexForAnchor(IItemTemplateResolver db, OptimizerState state, Config config, string anchorItemName, Random rng)
    {
        var t = db.GetTemplate(anchorItemName);
        if (t == null) return PickShapeIndex(state, config, rng);
        int size = t.Size switch
        {
            ItemSize.Small => 1,
            ItemSize.Medium => 2,
            ItemSize.Large => 3,
            _ => 0,
        };
        if (size == 0) return PickShapeIndex(state, config, rng);

        var indices = new List<int>();
        var weights = new List<double>();
        for (int i = 0; i < Shapes.All.Count; i++)
        {
            if (!Shapes.All[i].Contains(size)) continue;
            indices.Add(i);
            var counts = ComboSignature.ShapeCounts(Shapes.All[i]);
            weights.Add(state.Priors.ShapeWeight(counts));
        }
        if (indices.Count == 0) return PickShapeIndex(state, config, rng);

        // anchored 模式下仍允许少量探索：用 ExploreMix 决定是否均匀选一个 shape
        if (rng.NextDouble() < Math.Clamp(config.ExploreMix, 0.0, 1.0))
            return indices[rng.Next(indices.Count)];

        return indices[WeightedPick.PickIndex(weights, rng)];
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

            var deck = DeckGen.RandomDeckWeightedSynergy(
                shape,
                pool,
                state.Priors,
                db,
                rng,
                config.ExploreMix,
                config.SynergyPairLambda,
                config.SynergyMechanicLambda,
                config,
                state.TotalGames);
            if (deck == null) continue;

            var comboSig = ComboSignature.FromDeckRep(deck);
            if (state.Pool.ContainsKey(comboSig)) continue;

            var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, deck, state, config, pool, simulator, db, rng);
            var rep = ensured.Representative;
            var isConfirmed = ensured.IsConfirmed;

            var opps = EloSystem.SelectOpponentSignatures(state, config, isNewDeck: true, null, Math.Max(1, config.GamesPerEval * 2));
            if (opps.Count == 0)
                EloSystem.TryAddToPool(state, config, comboSig, rep, config.InitialElo, isConfirmed: isConfirmed);
            else
                EloSystem.RunGamesAndUpdateElo(comboSig, rep, opps, state, config, simulator, db);
            if (state.Pool.TryGetValue(comboSig, out var entry))
                state.Priors.ObserveCombo(
                    entry.Representative,
                    entry.Elo,
                    config,
                    db,
                    isConfirmed: entry.IsConfirmed,
                    gameCount: entry.GameCount);
            added++;
        }
    }

    /// <summary>
    /// 池内随机复测（联赛）：抽取若干“高风险虚高”的组合做少量复测，更新 ELO 并用结果纠偏 priors。
    /// </summary>
    private static void ReratePool(SimulatorClass simulator, IItemTemplateResolver db, ItemPool pool, OptimizerState state, Config config, Random rng)
    {
        if (config.RerateIntervalClimbs <= 0) return;
        int batch = Math.Max(0, config.RerateBatchSize);
        int budget = Math.Max(0, config.RerateGamesPerDeck);
        if (batch == 0 || budget == 0) return;
        if (state.Pool.Count < 2) return;

        // 优先复测：未确认 + 高分 + 低对局数（更可能虚高/不稳定）
        var candidates = state.Pool.Values
            .OrderByDescending(e => e.IsConfirmed ? 0 : 1)
            .ThenByDescending(e => e.Elo)
            .ThenBy(e => e.GameCount)
            .Take(Math.Min(state.Pool.Count, batch * 5))
            .Select(e => e.ComboSig)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (candidates.Count == 0) return;
        candidates = candidates.OrderBy(_ => rng.Next()).Take(batch).ToList();

        foreach (var sig in candidates)
        {
            if (!state.Pool.TryGetValue(sig, out var entry)) continue;
            var opps = EloSystem.SelectOpponentSignaturesForRerate(state, config, entry.Elo, M: Math.Max(1, Math.Min(6, budget)), rng);
            opps = opps.Where(s => s != sig).Take(Math.Max(1, Math.Min(6, budget))).ToList();
            if (opps.Count == 0) continue;

            var newElo = EloSystem.RunGamesAndUpdateEloBudget(sig, entry.Representative, opps, budget, state, config, simulator, db, rng);
            if (state.Pool.TryGetValue(sig, out var updated))
            {
                state.Priors.ObserveCombo(
                    updated.Representative,
                    newElo,
                    config,
                    db,
                    isConfirmed: updated.IsConfirmed,
                    gameCount: updated.GameCount);
            }
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
                var deck = DeckGen.RandomDeckWeightedSynergy(
                    shape,
                    pool,
                    state.Priors,
                    db,
                    rng,
                    config.ExploreMix,
                    config.SynergyPairLambda,
                    config.SynergyMechanicLambda,
                    config,
                    state.TotalGames);
                if (deck == null) continue;
                var comboSig = ComboSignature.FromDeckRep(deck);
                if (state.Pool.ContainsKey(comboSig)) continue;
                var ensured = RepresentativeSelector.EnsureRepresentative(comboSig, deck, state, config, pool, simulator, db, rng);
                EloSystem.TryAddToPool(state, config, comboSig, ensured.Representative, config.InitialElo, isConfirmed: ensured.IsConfirmed);
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
        // shape 仅作为尺寸约束：不再学习/加权选择，避免形状计数被早期样本锁死。
        return rng.Next(Shapes.All.Count);
    }
}
