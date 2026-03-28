# GreedyDeckFinder 性能观察与调优备忘（给后续 Agent）

本文整理 `BazaarArena.GreedyDeckFinder` 的性能结构、`--perf` 指标含义、常见误判与优化优先级，便于接手问题时快速对齐上下文。功能与参数仍以 [greedy-deck-finder.md](./greedy-deck-finder.md) 为准。

---

## 1. 时间主要花在哪里

- **绝对大头**：`BattleSimulator.Run`（单局模拟）。阶段上的「扩展 / 瑞士 / 大循环」墙钟里，绝大部分是 **嵌套在 `PlayBoNBatch` / `PlaySeriesBatch` 里的对战**。
- **预扁平化**：`GreedyPreflattenedResolver` 在进程内一次性处理模板，相对整次搜索通常可忽略。
- **`Deck` 缓存**：`BattleEvaluator` 用 `ConcurrentDictionary<Signature, Deck>` 避免重复构建；热点在 `GetOrAdd`，一般次于模拟本身。

---

## 2. `--perf` 与 `[性能·分解]` 怎么读

### 2.1 第一块（原有）

- **扩展 / 代表排列 / 瑞士轮 / 大循环 / 加赛**：各阶段 **墙钟**（`Stopwatch`）。注意 **代表排列** 的时间已包含在 **扩展** 的统计口径里（实现上 `repTicks` 是扩展内子段）；**加赛** 仅最后一档 `size` 上 `ResolveFinalTopTieByPlayoff` 整段。
- **BO 耗时 / 单局模拟耗时**：`BO` 为整段 BO 系列计时；**单局模拟** 为各线程对每场 `PlaySingleGameResult` 的耗时做 **Interlocked 累加**（**线程累计 CPU·时间**），不是另一段与阶段互斥的墙钟。
- **墙钟耗时**：从 `new PerfStats()` 到 `BuildSummary()`（在 `Program` 里位于 `RegisterAll` 之后、`GreedySearcher.Run` 为主），可视为 **一次完整搜索** 的进程墙钟。
- **吞吐(线程累计)**：`单局数 / (单局累计秒)`；**吞吐(墙钟)**：`单局数 / perf 墙钟秒`。

### 2.2 `[性能·分解]`（定量拆分）

| 指标 | 含义 |
|------|------|
| **阶段墙钟合计(扩+瑞+循+加)** | 四段阶段毫秒之和，应与上一行各阶段加总一致（量级）。 |
| **胶水合计** | **严格不含** `PlayBoNBatch` / `PlaySeriesBatch` 的编排：扩展桶、擂台初始化、擂台波次间（组对+合并存活）、瑞士（剪枝/分桶/建对/写分/排序）、大循环、加赛中的同类工作。 |
| **BO 并行/串行批** | 每批 **墙钟**、批次数、**对局数**。串行路径：`workers≤1` 或本批 **仅 1 对**（`ParallelPairsMinCount=2`）。 |
| **系列赛批** | 同上，对应 `PlaySeriesBatch`。 |
| **胶水+各批墙钟 vs 阶段合计偏差** | 应 **≈0%**。明显偏大说明有未计入区间或计时遗漏。 |
| **并行 BO 墙钟占阶段** | 并行 `PlayBoNBatch` 墙钟 / 阶段合计。 |
| **单局线程累计/perf墙钟** | 粗看 **批内有效并行度**：**>1** 表示多核在叠单局；接近 **1** 表示墙钟上几乎像在单流跑单局。 |
| **`[性能·批大小]`** | **BO/系列赛** 并行批与串行批各自的 **对数/批均值、最小、最大**；并行批 **对数\<workers 的批占比**（批内上限为 `min(对数, workers)`）；**分布** 为直方图（仅非零桶，`2×120` 表示每批 2 对的出现次数 120；`≥33` 为合桶）。 |

**实现位置**：`PerfStats.BuildSummary`；埋点分布在 `BattleEvaluator`（批墙钟）、`GreedySearcher`（扩展桶/擂台/加赛胶水）、`SwissTournament`（瑞士/大循环胶水）。

---

## 3. 关键经验：胶水极低 ≠ 端到端能接近线性加速

### 3.1 「胶水」不是 Amdahl 里的全部串行份额

- **胶水**只量 **CPU 在编排** 的几毫秒级时间。
- **算法强制顺序**（必须等本批对战结束才能算下一批对局）在墙钟上体现为 **多段「对战批墙钟」首尾相接**，这段时间算在 **批墙钟**里，**不算进胶水**。
- 因此会出现：**胶水占比 <1%**，但 **`workers=1` vs `workers=8` 整次 run 加速仍可能只有 ~1.2×～1.3×**（且常因路径分叉不可严格对比）。

### 3.2 批与批串行是主要结构限制

- **擂台** `RunBatchedKnockoutMany`：`while` **每一波** 先胶水组对 → `PlayBoNBatch` → 胶水合并；**波次之间不能并行**。
- **瑞士**：每轮一轮 `PlayBoNBatch`；**轮次之间不能并行**。
- **`size` 外层循环**：后一 size 依赖前一 size 的 TopK，**不能并行**。

批 **内部** 可 `Parallel.For`；批 **之间** 在墙钟上是一条长链。总墙钟 ≈ **各批墙钟之和** + 少量胶水。

### 3.3 批内并行度：何时受批大小限制

- **上界恒成立**：批内有效并行路数 ≤ `min(对数, workers)`。
- **大规模搜索下的实测**（`--level 4`、`--workers 8`、默认 TopK/M，锚点如狼筅）：**BO 并行批** 的 **对数/批均值** 常 **远大于 8**，**对数\<workers 的并行批占比** 常为 **个位数 %**（见 **`[性能·批大小]`**）。此时 **批尺寸较少成为「吃不满 8 核」的主因**；仍会有 **2～3 对** 的小批（擂台/瑞士末段等），属结构常态。
- **仍可能拉满单批墙钟的因素**：**`BO>1`** 时每任务多局串行，整批墙钟受 **最慢任务** 支配；**`Parallel.For` 栅栏** 须等最慢结束。批内 **随机打乱对局顺序** 用于缓解与顺序相关的负载不均（见 §4）。

### 3.4 对比 `workers=1` 与 `workers>1` 时的陷阱

- 模拟器内仍有 **`ThreadLocalRandom`**（如暴击）时，**并行与串行路径对战结果可能不同** → **搜索树分叉** → **总对局数/批次数可能不同**。  
- 因此 **不宜用「单次墙钟比」断言并行效率**；要看 **`[性能·分解]`**、**同参数多次跑的中位数/区间**，或只测 **孤立 `PlayBoNBatch`**。
- **项目约定**（见 `.cursor/rules/greedy-deck-finder.mdc`）：贪心搜索以 **统计意义** 为主，**不要求** 并行下每场随机流比特级可复现；**不得** 为严格可复现阻碍并行调度优化（如批内打乱）。

### 3.5 `单局线程累计` 与 `墙钟`

- **线程累计 ≫ 墙钟**：批内多核叠跑，正常。
- **线程累计 ≈ 墙钟**（在 `workers>1` 时）：说明墙钟上 **重叠很少**，常见于 **大批次极少、或大量串行批/单对批**。

### 3.6 单实例 `BattleSimulator` 与 `Run` 的线程安全（架构经验）

- **为何共用一个模拟器**：`Program` 只构造 **一个** `BattleSimulator` 交给 `BattleEvaluator`。并行来自 `PlayBoNBatch` / `PlaySeriesBatch` 里的 **`Parallel.For`**，每个任务仍调用 **同一 `_simulator` 上的 `Run`**，而不是「每线程 `new BattleSimulator()`」。目的是共享 **`PrepareDeck` 缓存**、控制分配；代价是 **`Run` 必须按多线程重入设计**。
- **典型错误**：把 **`BuildSessionTables` 的结果**（或任何「本场对局专属」表）存进 **`BattleSimulator` 实例字段**，再赋给 `battleState`。并发两场 `Run` 时，后一场会覆盖字段，先一场若仍用错表遍历能力/光环，会出现 **id 与盘面不一致**、甚至 **`GetAura` 索引越界**。
- **正确方向**：本场会话表用 **`Run` 内局部变量**，仅赋给本场 `BattleState.SessionTables`；共享的 `_preparedDeckCache` 用 **`lock`（或等价并发结构）** 保护读写。
- **与优化的关系**：`ItemState` 原型（§4）减少构图成本；**会话表局部化 + 缓存加锁** 保证并行正确。若未来改为 **每线程一个 `BattleSimulator`**，可弱化对 `Run` 跨调用隔离的要求，但需单独设计缓存共享策略。

---

## 4. 已做过的优化（便于对照代码）

- **瑞士剪枝**：数学上不可能进前 `K*M` 的候选移除；`--perf` 有 **瑞士剪枝淘汰** 累计人次。
- **瑞士单轮合并**：整轮所有分数桶对局 **一次** `PlayBoNBatch`，避免按桶小批无法并行。
- **并行阈值**：`BattleEvaluator` 中 `ParallelPairsMinCount = 2`（≥2 对且 `workers>1` 才 `Parallel.For`）。
- **随机数**：瑞士子流（主 `Random` 每进瑞士只取 1 个种子）；`--seed` 时并行批内对局可用 **派生 `Random`** 做先后手/平局掷币；**批内对局顺序随机打乱** 时槽位与种子对应关系会变。**不追求** 与串行/旧版比特级一致（见 `greedy-deck-finder.mdc`）。
- **Greedy 物品 `ItemState` 原型**（`IItemBattlePrototypeResolver` + `GreedyPreflattenedResolver`）：搜索启动时对池内每件物品按 **`GreedyLevelRules.CombatTier(playerLevel)`** 构造对应档位 `ItemState` 原型；`BattleSimulator.PrepareDeck` 在「**战斗档位与原型一致**、无 overrides、解析器提供原型」时用 `new ItemState(proto)`（`Array.Copy`）代替对扁平模板逐键 `GetInt`，减少卡组缓存未命中时的构造开销。
- **并行 `Run` 正确性**：`BattleSimulator` 原先将 `BuildSessionTables` 结果写入实例字段后再赋给本场 `BattleState`，多线程共用一个模拟器时会发生表与战场错配；已改为 **每场对局的局部 `sessionTables` 变量**，且 **`PrepareDeck` 缓存读写加锁**，避免并行下字典损坏或未定义行为。

---

## 5. 优化优先级建议（给决策用）

0. **`单局线程累计/perf墙钟` 已接近 `workers`、且 `[性能·批大小]` 显示大批为主**（与 §8 基线同类）  
   → **默认认为并行批内利用已较充分**；新增工作 **不要** 以「再叠一层并行编排」为默认项，除非指标恶化或场景明显变小（小池、极小 TopK 等）。

1. **`胶水占比极低` 且 `并行 BO 墙钟占阶段` 极高**  
   → 再抠 **并行框架/胶水** 回报很小；优先 **优化 `BattleSimulator.Run`** 或 **减少对战次数**（`top-k`、`top-multiplier`、`--bo`）。

2. **`BO 串行对占比` 或 `BO 串行批墙钟` 明显偏高**  
   → 再考虑 **合并批次**、**并行阈值**、或 **算法上减少批次数**（需保持语义正确，禁止掩耳盗铃式特例）。

3. **需要可对比的并行基准**  
   → 同参数 **多次墙钟** 取中位数/区间；若必须严格可复现，用 **独立微基准** 或接受仅主流程 seed 语义，**勿** 在 GreedyDeckFinder 并行路径上为比特级可复现牺牲调度（见 `greedy-deck-finder.mdc`）。

4. **验证**  
   - Release：`dotnet build ... -c Release`  
   - 示例：`--anchor-item 狼筅 --level 4 --perf --workers 8`（`level 4` 时池子为 **Vanessa 铜**、战斗扁平化为铜档；锚点须在该池内；高等级时池含更高 `MinTier`，见 `GreedyLevelRules`）

---

## 6. 相关源码路径（快速跳转）

| 内容 | 路径 |
|------|------|
| 性能汇总与分解输出 | `src/BazaarArena.GreedyDeckFinder/PerfStats.cs` |
| 批并行与批墙钟 | `src/BazaarArena.GreedyDeckFinder/BattleEvaluator.cs` |
| 扩展、擂台胶水、加赛 | `src/BazaarArena.GreedyDeckFinder/GreedySearcher.cs` |
| 瑞士/大循环胶水 | `src/BazaarArena.GreedyDeckFinder/SwissTournament.cs` |
| 入口与 `PerfStats` 生命周期 | `src/BazaarArena.GreedyDeckFinder/Program.cs` |
| 单局模拟 | `src/BazaarArena/BattleSimulator/BattleSimulator.cs` |
| 铜档战斗原型解析器 | `src/BazaarArena/BattleSimulator/IItemBattlePrototypeResolver.cs` |
| Greedy 扁平模板 + 原型 | `src/BazaarArena.GreedyDeckFinder/GreedyPreflattenedResolver.cs` |

---

## 7. 与 CLI/测试规则的关系

完整测试档位与脚本见 [cli-and-testing.md](./cli-and-testing.md) 与 `.cursor/rules/cli-and-testing.mdc`。GreedyDeckFinder 是 **独立控制台项目**，性能文档 **不替代** 物品测试流程。

---

## 8. 经验结论与参考基线（并行性）

**结论（给后续 Agent）**

- 在 **Vanessa 铜、`--level 4`、默认 `top-k`/`top-multiplier`、`--workers 8`** 一类 **大规模** 跑法下，`--perf` 显示 **`单局线程累计/perf墙钟` ≈ 6.5～7**，表明 **多核在批内叠跑单局已较充分**；与 `.cursor/rules/greedy-deck-finder.mdc` 中「勿把并行框架当首要优化」的立场一致。
- **端到端墙钟** 仍受 **算法固有的批间顺序**、**总单局数**、**模拟器热点** 等约束；**胶水占比小** 不能理解为「批与批已并行」。
- **同参数多次运行**：墙钟、单局数可能因路径分叉 **波动数个百分点级或更多**，对比优化应用 **多次中位数/区间**。

历史具体数值样例（不同机器、不同提交、不同随机路径下的墙钟与吞吐）已移除，避免把过时数据当成固定基线。后续请按当前代码与机器环境重新测量，并以同参数多次运行的中位数/区间做对比。

---

## 9. `BattleSimulator.Run` 基准工程（`BazaarArena.Benchmarks`）

- **工程**：`src/BazaarArena.Benchmarks`，已加入 `bazaar-arena.sln`。
- **BDN**：主程序为 **net10.0-windows**（与 WPF 主工程一致），配置为 **InProcessEmit** + `ConfigUnionRule.AlwaysUseLocal`，避免默认子工程 `net10.0` 与主工程 **NU1201** 不兼容；勿依赖会并入 **Default 工具链** 的命令行 `--job`（若需改迭代次数，改 `BenchmarkConfig.cs` 内 `Job`）。
- **运行**（仓库根目录）：`dotnet run -c Release --project src/BazaarArena.Benchmarks/BazaarArena.Benchmarks.csproj` — BenchmarkDotNet 表（Mean、分配等）。
- **默认场景**：双方均为 **10 槽**（`PlayerLevel=5`）、全铜；参考卡组1 为 **9 个槽位条目**（中型「鲨齿爪」占 2 槽，与其余小型合计 10 槽）vs 参考卡组2（10 件小型）。卡组定义见 `TenSlotDeckScenarios.cs`。
