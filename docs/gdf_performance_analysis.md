# GDF（`bazaararena_gdf`）性能瓶颈分析

本文基于 **C++ 实现与 legacy C# `BattleEvaluator` 的对照阅读** 归纳主要瓶颈，便于后续 profiling（Sampling CPU、VTune、`/Bt+` 等）时对照验证。**未改核心 `Simulator.cpp` 帧序**；优化集中在 GDF 评估层与并行策略。

**当前实现状态（与下文「历史瓶颈」对照）**：为优先极致性能，GDF **不保证对战 RNG 可复现**（由 `Simulator` 保证对局语义正确即可）。已落地：`thread_local` 每线程复用一个 `Simulator` + 每局 `InitializeSimulator`（避免每局默认构造整颗模拟器树）、批盐 + 子种子混合、串行/并行路径均**不再**在热路径调用 `std::random_device`；`deck_cache_` 使用 `shared_mutex` 保护并行 `ToSide`；CLI **省略 `--workers` 时默认 `hardware_concurrency()`**。仍待观察/可选：**P1** 每批 `std::thread` 创建（见第 4 节）。

---

## 1. 结论摘要（按影响优先级）

| 优先级 | 瓶颈 | 说明 |
|--------|------|------|
| **P0**（已缓解） | **每局新建 `Simulator` + 全量 `InitializeSimulator`** | 历史：C++ 在热路径**栈上** `core::Simulator sim` 每局默认构造整颗对象。当前：`thread_local` 复用单实例，每局仅拷贝 `sides`、重置 `sandstorm`/sink 后 `InitializeSimulator` + `Run`（见第 2 节新引用）。 |
| **P1** | **每批对战 `join` 一批全新 `std::thread`** | `PlayBoNBatch` / `PlaySeriesBatch` 每次调用创建 `std::vector<std::thread>`，按 chunk 跑完即 `join` 销毁。C# `Parallel.For` 使用**线程池**。瑞士轮多轮、扩展多档会放大该成本；若 profiling 仍显示线程 API 占比高，可考虑持久线程池。 |
| **P2**（已缓解） | **热路径 `std::random_device`** | 历史：串行系列赛在缺省种子时每 pair `random_device`。当前：批级盐与 `FastEntropyU64` 等混合派生 `game_word` / `pair_seed`，无 `random_device` 热路径。 |
| **P3** | **`SideState` 整结构拷贝** | 每局 `sim.sides[0] = side_a` 仍为完整拷贝；与「每局独立 RNG 与位图」一致，属必要成本。 |
| **—**（已变） | **默认并行度** | 当前 CLI **省略 `--workers` 时默认硬件并发**；显式 `--workers 0` 才整段对战单线程。与 legacy 默认可能不一致，跨语言对比时请对齐线程数。 |

---

## 2. 证据：当前 C++ 每线程复用模拟器

[`BattleEvaluator.cpp`](../engine/src/bazaararena/gdf/BattleEvaluator.cpp)：`thread_local` 懒创建 + 每局 `RunSingleBattleReturn` 内拷贝两侧、播种、`InitializeSimulator`、`Run`：

```42:63:engine/src/bazaararena/gdf/BattleEvaluator.cpp
core::Simulator& TlsBattleSimulator() {
    thread_local core::Simulator sim;
    return sim;
}

static int RunSingleBattleReturn(core::Simulator& sim, const core::SideState& side_a, const core::SideState& side_b, int swap, int rng_seed) {
    sim.sink.sink_type = io::Sink::TypeNone;
    sim.sink.max_events = 0;
    sim.sink.truncated = false;
    sim.sink.Clear();

    if (swap == 0) {
        sim.sides[0] = side_a;
        sim.sides[1] = side_b;
    } else {
        sim.sides[0] = side_b;
        sim.sides[1] = side_a;
    }
    sim.sandstorm = core::Simulator::SandStorm{};
    sim.rng.Seed(rng_seed);
    core::InitializeSimulator(sim);
    return sim.Run(true);
}
```

[`Simulator.hpp`](../engine/include/bazaararena/core/Simulator.hpp) + [`AbilityQueue.hpp`](../engine/include/bazaararena/core/AbilityQueue.hpp)：`AbilityQueue::MaxQueueSize = 4096` 等仍使**单实例**体积较大；复用后避免**每局**默认构造整颗对象，但每局仍需 `InitializeSimulator` 重建位图（与引擎设计一致）。

---

## 3. 证据：C# 复用单例模拟器

[`BattleEvaluator.cs`](../legacy/dotnet/src/BazaarArena.GreedyDeckFinder/BattleEvaluator.cs)：

```210:226:legacy/dotnet/src/BazaarArena.GreedyDeckFinder/BattleEvaluator.cs
    private int PlaySingleGameResult(
        DeckRep a,
        DeckRep b,
        Random? dedicatedRng,
        bool useTlsWhenDedicatedNull,
        bool randomJudgeOnAbsoluteDraw)
    {
        _perf.IncSingleGame();
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var deckA = ToDeck(a);
        var deckB = ToDeck(b);
        int swap = Next2(dedicatedRng, useTlsWhenDedicatedNull);
        Deck d0 = swap == 0 ? deckA : deckB;
        Deck d1 = swap == 0 ? deckB : deckA;

        var sink = new HpTrackingSink();
        int winner = _simulator.Run(d0, d1, _db, sink, BattleLogLevel.None);
```

`_simulator` 为构造 `BattleEvaluator` 时注入的**同一实例**，不在热路径上反复 `new` 整个模拟器内核。

---

## 4. 证据：每批并行都新建线程

[`BattleEvaluator.cpp`](../engine/src/bazaararena/gdf/BattleEvaluator.cpp) `PlayBoNBatch` / `PlaySeriesBatch` 在 `workers_ >= 2` 且 `pairs.size() >= 2` 时：

```198:219:engine/src/bazaararena/gdf/BattleEvaluator.cpp
    const auto order = CreateShuffledOrder(pairs.size(), order_rng);
    const long long batch_id = ++parallel_batch_seq_;
    std::vector<std::thread> threads;
    const int wcount = std::min(workers_, static_cast<int>(pairs.size()));
    const size_t chunk = (pairs.size() + static_cast<size_t>(wcount) - 1) / static_cast<size_t>(wcount);
    for (int t = 0; t < wcount; t++) {
        // ...
        threads.emplace_back([&, beg, end]() {
            for (size_t k = beg; k < end; k++) {
                // ...
            }
        });
    }
    for (auto& th : threads) th.join();
```

C# 对应路径为 `Parallel.For(..., MaxDegreeOfParallelism = _workers)`（线程池）。GDF 一次完整搜索会触发**成百上千次**批调用（扩展、瑞士轮每轮、循环赛、锚点增广、淘汰赛、playoff），线程创建/销毁累积显著。

---

## 5. 串行路径上的 `random_device`（历史问题）

旧版在 `PlaySeriesPointsCore(..., std::nullopt)` 分支对**每个 pair** 调用 `std::random_device` 播种本地 `mt19937`，在 Windows 上成本很高。当前实现已移除该路径：系列赛由 `PlaySeriesPointsForPair` 接收 `pair_seed`，并由批级盐与索引混合出各局的 `game_word`（见 `BattleEvaluator.cpp` 中 `FastEntropyU64` / `NextBatchSaltU64`）。

---

## 6. 建议的验证手段（Profiling）

1. **Sampling**：对 `bazaararena_gdf.exe` 采 CPU 样，预期热点：`InitializeSimulator`、`Simulator::Run`、`std::thread::` 构造/join（若 P1 显著）。
2. **A/B**：同一搜索参数，改 `--workers 0` vs 物理核数，观察墙钟与 CPU 利用率（`--seed` 仅影响搜索用 RNG，**不**使对战 bitwise 可复现）。
3. **计数**：在 `BattleEvaluator` 内临时统计 `Run` / `InitializeSimulator` / `PlaySeriesBatch` 调用次数（或对接现有 perf 钩子），与理论对局数核对。

---

## 7. 优化方向（实现时需注意语义）

| 方向 | 思路 | 风险/注意 |
|------|------|-----------|
| **复用 `Simulator`** | ✅ 已用 `thread_local`；每局重置 `sandstorm`、sink、拷贝 `sides` 后 `InitializeSimulator` | GDF 明确**不**追求对战可复现；若有回归需对照 `Simulator` 语义而非逐 bit RNG。 |
| **线程池** | 固定 `N` 个 worker + 任务队列，避免每批 `std::thread` | 实现成本与任务粒度权衡；若 P1 在 profile 中不明显可暂缓。 |
| **减少 `random_device`** | ✅ 热路径已去除 | — |
| **默认 workers** | ✅ CLI 省略时 `hardware_concurrency()` | 与 legacy 默认可能不同；文档已说明。 |

---

## 8. 与「比 C# 更慢」的对照检查清单

- [ ] C# 运行时是否使用了 **更高 `Workers`** 或 Release JIT，而 C++ 使用 `--workers 0` / Debug 构建。
- [ ] 是否在对比 **λ>0 的 C++**（多约 **3×** 系列赛局数）与 **legacy 无锚点增广**。
- [ ] 是否在相同 **等级 / k / M / BO** 下对比。

完成上述对齐后，再用本节的 P0–P2 项做针对性 profiling，通常能解释大部分墙钟差异。
