# 优质卡组探测器 性能分析报告（阶段用时 & 多线程效果）

更新时间：2026-03-18  
适用对象：`src/BazaarArena.QualityDeckFinder`

本报告基于在探测器中新增的 `--perf` 统计输出（每赛季打印各阶段耗时、对局吞吐与对战模拟成本），对“第 1 个赛季运行很慢、`--workers 14` 仍不明显”的现象做阶段拆解与并行效率分析，为后续优化提供方向。

---

## 结论摘要（最重要的三点）

- **整体慢的主因不是匹配赛，而是“卡组优化（HillClimb）”**：锚定 HillClimb 对局量最大，通常占赛季耗时的绝大部分，优先优化它才有结构性收益。
- **`--workers` 的收益取决于并行覆盖范围**：目前已将 HillClimb 评估对局纳入并行模拟（仍按原顺序单线程应用 ELO/写池），并对 `BuildSide` 引入精简克隆以降低分配；因此 `--workers 14` 的赛季墙钟出现明显下降，但加速仍远小于 14×。
- **并行下单局成本可能上升**：即使没有共享写，仍会受 GC/内存带宽/调度影响；此外统计口径为“Run() 总耗时 / 次数”时，可能观察到均值上升。

---

## 统计口径与如何复现

### 开启性能统计

运行时加 `--perf`，每个赛季结束会打印：

- **代表选择**耗时
- **匹配赛**：赛程构造 / 跑局 / 合并写池耗时、轮数、匹配局数
- **卡组优化**：强度 HillClimb / 锚定 HillClimb 耗时
- **放弃/注入**耗时
- **对战模拟**：`BattleSimulator.Run()` 总耗时与均值

示例命令（跑 1 个赛季后退出，便于采样）：

```bash
dotnet run -c Release --project src/BazaarArena.QualityDeckFinder/BazaarArena.QualityDeckFinder.csproj -- \
  --max-seasons 1 --top-interval 1 --save-interval 1 --perf --workers 14
```

### 本报告使用的基准参数（小规模，用于阶段占比与并行效率）

为了把“赛季内所有阶段都跑到、且 1 个赛季在几十秒内结束”，使用了较小的预算：

- `--season-match-cap 10`
- `--max-climb-steps 50`
- `--neighbor-sample 20`
- `--mab-budget 10`
- `--games-per-eval 5`

说明：

- 这些参数**不代表真实生产配置**，但足以暴露结构性瓶颈：HillClimb 占比高；`--workers` 的收益取决于「对局评估并行覆盖范围」与「每局 BuildSide 分配/克隆成本」。
- 真实配置（默认 `max-climb-steps=500`、`neighbor-sample=80`、`mab-budget=30` 等）会把 HillClimb 的对局量放大到数量级更高，因此“第 1 赛季可能 10 分钟”是合理的。

---

## 阶段拆解：赛季内部在做什么、耗时落在哪里

一个虚拟赛季（`Runner.RunSeason`）可按实现切分为：

1. **代表选择**：锚定代表抽样、合并活跃玩家集合（极快）
2. **匹配赛**（可并行）：
   - 构造本轮赛程（挑对手签名）
   - 跑对局（调用 `BattleSimulator.Run()`）
   - 单线程按结果更新 ELO/对局数、写回池
3. **卡组优化 HillClimb（强度）**：对强度玩家逐个做一次爬山；邻域采样 + 评估（对局）→ 找到首次改进/预算耗尽。每次“评估”内部的对局模拟可并行（但 ELO/写池仍按原顺序单线程应用）。
4. **卡组优化 HillClimb（锚定代表）**：对本季抽样到的锚定代表逐个爬山（通常数量更大，且是本赛季最大头）。评估对局并行机制同上。
5. **放弃/注入**：重启锚定/注入新强度玩家（通常很快）

关键点：现已将 **HillClimb 的对局评估**也并行化（仍保持写 state 的顺序语义），并将 `BuildSide` 从“深拷贝能力/条件树”改为“扁平化 `_intsByTier` 的精简克隆”，以降低每局分配与克隆成本。

---

## 实测数据（1 赛季，小规模预算）

> 注：每次运行会因为随机种子/初始玩家数/卡组变化导致抖动；这里更关注“占比与趋势”。（已包含：P0 并行评估 + 并行热路径 thread-local RNG + P1b 禁用外战确认与代表缓存）

### A. `--workers 0`（单线程）

（方案 1 + BuildSide 精简克隆：workers=0 等价于不开并行）

- **赛季总耗时**：10.3s  
- **赛季对局增量**：8323  
- **吞吐**：806.7 局/秒  
- **匹配赛跑局**：0.721s（≈598 局）  
- **HillClimb（强度）**：1.546s  
- **HillClimb（锚定）**：8.036s  
- **`BattleSimulator.Run()` 均值**：1.196ms/局

占比粗算：

- 匹配赛跑局约 2.8%
- 锚定 HillClimb 约 81%
- 强度 HillClimb 约 16%

### B. `--workers 14`（并行匹配赛 + HillClimb 评估对局批量并行）

（方案 1 的“正确并行粒度” + BuildSide 精简克隆：评估对局展开为列表并行模拟，再按原顺序单线程应用 ELO/写池）

- **赛季总耗时**：5.6s  
- **赛季对局增量**：9265  
- **吞吐**：1646.0 局/秒  
- **匹配赛（并行）跑局**：0.379s（≈600 局）  
- **HillClimb（强度）**：0.810s  
- **HillClimb（锚定）**：4.425s  
- **`BattleSimulator.Run()` 均值**：2.299ms/局（注意：并行下“单局均值”会因争用变大）

观察：

- **HillClimb 吃到并行收益**：锚定优化约 8.0s → 4.4s（约 **1.82×**），赛季总耗时约 10.3s → 5.6s（约 **1.83×**）。
- **`Run()` 合计耗时可能大于赛季墙钟耗时**：这是正常现象——并行时多个线程的 `Run()` 时间在墙钟上重叠，统计的是“CPU 时间总和”，不是墙钟时间。
- 仍存在 **并行争用**：并行下 `Run()` 的“每局均值”会上升（2.299ms/局 vs 1.196ms/局），因此加速比远小于 workers 数；后续要进一步提升，需要减少模拟器分配/争用（见 P1）。

---

## 为什么 `--workers 14` 仍然不是 14×（机制解释）

### 1) 主要耗时仍在 HillClimb，且并行加速受限于单局成本上升

当前实现已将 HillClimb 评估对局纳入并行（`EloSystem.RunGamesAndUpdateElo*`：并行模拟 winners + 顺序写回 ELO/池），因此“并行覆盖范围太小”这一类问题已基本解决；但在 `--workers 14` 下，`BattleSimulator.Run()` 的**单局均值会明显上升**（GC/内存带宽/调度等争用），导致整体加速比远小于 worker 数。

因此当默认参数较大时：

- 匹配赛对局数上限 \(\approx \text{活跃玩家数} \times \text{SeasonMatchCap}\)
- HillClimb 对局数则大致与
  - `MaxClimbSteps`
  - `NeighborSampleSize`
  - `MabBudgetPerStep`
  - `GamesPerEval`
  - 活跃玩家数（强度 + 锚定代表数）
  成乘法关系增长

在 P1b 落地后，HillClimb 的“非对局开销”（尤其代表排列外战确认）已基本消失，当前进一步提速的主要方向回到：**降低每局模拟的分配/争用**（见 P1），让并行的“每核效率”上来。

### 2) 并行不是免费午餐：每局平均成本会上升

在并行匹配赛中会出现：

- 多线程同时构建战斗状态（大量对象分配）
- GC 压力上升、内存带宽竞争
- 线程调度开销（`Parallel.For` + 分块结果合并）

导致 **每局 `BattleSimulator.Run()` 平均耗时上升**，从而抵消一部分并行收益。

---

## 进一步细分：HillClimb 的真正瓶颈在哪里

为验证“HillClimb 慢到底慢在哪”，对以下热点做了墙钟拆分并在 `--perf` 输出：

- 邻域采样：`Neighborhood.SampleComboNeighborsWeighted`
- 洗牌/随机序：`neighbors.OrderBy(_ => rng.Next()).ToList()`
- 代表排列确保：`RepresentativeSelector.EnsureRepresentative`（协同先验爬山 + 缓存；不做外战确认）
- 选对手：`EloSystem.SelectOpponentSignatures`（HillClimb 用）
- 评估对局：`EloSystem.RunGamesAndUpdateElo*`（已含“构建对局/模拟对局/顺序写回”细分）

本节仅展示一次代表性跑数（会随随机种子略抖动）；核心关注“哪个子项是大头”。

### A. `--workers 0`（单线程，一次跑数）

- HillClimb 总墙钟：强度 1546ms + 锚定 8036ms
- HillClimb 评估细分：构建对局 7ms，**模拟对局 9244ms**，顺序写回 17ms
- HillClimb 其它细分：邻域采样 73ms，洗牌/随机序 2ms，EnsureRepresentative 32ms，选对手 17ms

结论：P1b 落地后，`EnsureRepresentative` 已不再是大头；**HillClimb 的墙钟主要由“模拟对局（Run）”构成**。

### B. `--workers 14`（并行，一次跑数）

- HillClimb 总墙钟：强度 810ms + 锚定 4425ms
- HillClimb 评估细分：构建对局 7ms，**模拟对局 4828ms**，顺序写回 20ms
- HillClimb 其它细分：邻域采样 62ms，洗牌/随机序 2ms，EnsureRepresentative 44ms，选对手 18ms

结论：并行后“模拟对局”明显变快，且 `EnsureRepresentative` 不再主导；此时 `--workers` 的收益主要被 **`BattleSimulator.Run()` 并行下单局成本上升**与调度/GC 争用限制。

---

## 当前统计的局限（避免误读）

- `--perf` 中的“构建 Deck(ToDeck/BuildSide)”目前统计的是 **`DeckRep.ToDeck()` 的时间**；真正重的部分往往在 `BattleSimulator.Run()` 内部的 `BuildSide`（克隆 `ItemTemplate/AbilityDefinition` 等），这部分时间已经包含在 `Run()` 的均值里，但不会单独拆出来。
- 单次跑 1 个赛季的数字会受随机性影响；如果要做更稳定的对比，建议固定 RNG 种子或跑多次取中位数/平均值。

---

## 后续优化建议（按优先级）

### P0（已落地）：把 HillClimb 的“对局评估”纳入并行（结构性收益最大）

已实现要点（不改变算法语义的前提下）：

- **把“对局评估”与“写池/更新 ELO/Priors”解耦**  
  先并行跑局（纯函数式：输入两套 deck → winner），收集结果；再由单线程按固定顺序应用结果更新 state。
- 对 HillClimb：评估时把需要跑的局按“原本顺序”展开为线性列表，先并行模拟 winners，再按顺序应用 ELO/写池，保证与单线程等价。

补充：并行热路径中将 `Random.Shared` 替换为线程局部随机数以降低争用。

落地后的现状：**HillClimb 仍是绝对大头**，并行加速开始受限于 `BattleSimulator.Run()` 的单局成本在多线程下上升（GC/内存带宽/调度），因此下一步更值得投入的是 P1。

### P1：降低每局模拟的分配/克隆成本（提升单局吞吐）

`BattleSimulator.Run()` 内每局都会：

- 为双方构建战斗侧
- 克隆模板、能力、条件、光环等结构

可以考虑：

- **精简克隆（已落地）**：`BuildSide` 已改为使用 `ItemTemplateBattleClone` 扁平化 `_intsByTier` 为单值，并共享 `Tags/Abilities/Auras` 引用，避免深拷贝能力/条件树。
- **QDF 启动期预扁平化（已落地）**：QDF 启动时会对“物品池范围内”的模板一次性扁平化为 Bronze 单值，并对可复写属性应用「Bronze 默认值的一半」，从而避免每局再次做 tier 映射/构造 overrides 字典；战斗中仍会为每件物品克隆一份可写模板以承载运行时变量。
- **复用临时集合**：减少 `new List<>` / `ToList()` / `OrderBy()` 等热点分配（例如帧循环内的临时集合）。

这会同时提升单线程与多线程的上限，并缓解并行下 `Run()` 均值上升的问题。

### P1b（已落地）：移除外战确认 + 代表缓存（原 HillClimb 最大头）

按设计文档约束（不做外战验证），已将 `EnsureRepresentative` 的外战确认移除，并新增 comboSig→代表排列缓存，避免在 HillClimb 中反复为同一组合做代表寻找。落地后 `EnsureRepresentative` 在 HillClimb 的墙钟占比已降到毫秒级，HillClimb 的主瓶颈回到对局模拟与其并行争用（见上文细分）。

### P2：降低 HillClimb 的“评估局数”而不显著降质量（算法层）

如果允许牺牲少量质量换速度：

- 降低 `GamesPerEval`（或改为自适应：早期少打、后期确认多打）
- 降低 `MaxClimbSteps` / `NeighborSampleSize` / `MabBudgetPerStep`
- 对锚定代表：分阶段（先粗筛、后精选），减少“对每个锚定代表都跑满预算”的情况

---

## 附：实现位置（便于继续深挖）

- 赛季主流程与阶段划分：`src/BazaarArena.QualityDeckFinder/Runner.cs`
- 并行匹配赛跑局：`Runner.RunMatchPhaseParallel` / `Runner.RunGamesParallel`
- HillClimb：`src/BazaarArena.QualityDeckFinder/HillClimb.cs`
- 对局评估与 ELO 更新：`src/BazaarArena.QualityDeckFinder/EloSystem.cs`
- 性能计数器（新增）：`src/BazaarArena.QualityDeckFinder/PerfCounters.cs`

