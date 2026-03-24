# 锚定贪心搜索器（GreedyDeckFinder）

`BazaarArena.GreedyDeckFinder` 是按锚定物品搜索 `size=6` 最强卡组的控制台程序。

## 核心规则

- 以锚定物品为起点，按物理占用 `size` 从 `sA` 递增到 `6`。
- 每轮 `size=s` 的候选来自所有分解 `s=p+q`（`q ∈ {1,2,3}`）：
  - `TopK[p] + 所有 size=q 物品`。
- 候选生成后立即选择该组合的代表排列：
  - 固定旧物品顺序，仅枚举新物品的所有插入位置；
  - 插入位置间使用擂台淘汰（King-of-hill）进行 BO 选择代表排列，减少比赛数量（仅依据对战结果比较插入方案，不另设独立启发式打分）。
- 候选筛选分两阶段：
  1. 瑞士轮：`ceil(log2(候选数))` 轮，同分桶随机配对，禁止重复交手；每轮开始前剪枝「当前分 + 剩余轮数」仍不可能进入前 `K*M` 名的候选，存活人数已 ≤ `K*M` 时提前结束瑞士轮；
  2. 大循环：在 `Top(K*M)` 中做 BO5 全对全，选出 `TopK`。
- 无平局：
  - 单局平局时按最终生命值（更接近 0 者胜）裁决；
  - 仍相同则随机。

## 运行方式

```bash
dotnet run --project src/BazaarArena.GreedyDeckFinder/BazaarArena.GreedyDeckFinder.csproj -- --anchor-item 鲨鱼爪 --top-k 10 --top-multiplier 3 --seed 1
```

常用参数：

- `--anchor-item <物品名>`：必填
- `--top-k <K>`：每轮保留的候选数量
- `--top-multiplier <M>`：瑞士轮后保留 `K*M` 进入大循环
- `--bo <n>`：BO*n*，默认 5（仅支持奇数）
- `--seed <n>`：随机种子，调节主流程与瑞士子流等；**不保证** 与并行调度细节（如批内对局顺序）组合后仍比特级可复现（见下「随机数」）
- `--workers <n>`：并行执行 BO 对战（0/1 为串行）
- `--perf`：输出阶段耗时、BO 数、单局数与吞吐（含代表排列候选数、代表排列 BO 数、瑞士剪枝淘汰人数累计）；另输出 **`[性能·分解]`**：严格串行「胶水」分项（扩展桶、擂台初始化/波次间、瑞士、大循环、加赛）、`PlayBoNBatch`/`PlaySeriesBatch` 并行与串行批的墙钟及对局数、**胶水+各批墙钟与阶段合计的核对偏差**（应接近 0%）、并行 BO 占阶段比例及 `单局线程累计/perf墙钟` 估计有效并行度。
- `--output <path>`：可选，结果写入文件
- `--exclude-item <物品名[,物品名...]>`：可重复传，用于在生成过程中始终排除指定物品（锚定物品不可排除）

## 性能实现说明

- 启动时会对物品池模板做一次性预扁平化（Bronze 单值化 + 按玩家等级的 overridable 预应用），并为池内每件物品构造铜档 **`ItemState` 只读原型**（`IItemBattlePrototypeResolver`）；`BattleSimulator.PrepareDeck` 在铜档且无槽位 overrides 时通过拷贝原型属性数组构图，减少重复 `GetInt`。
- **并行架构**：进程内通常只有 **一个** `BattleSimulator` 实例；`workers>1` 时多个线程同时调用其 `Run`。因此 `Run` 不得依赖会跨调用互相覆盖的实例级本场状态；`PrepareDeck` 缓存须线程安全。详见 [greedy-deck-finder-performance-notes.md §3.6](./greedy-deck-finder-performance-notes.md) 与 `.cursor/rules/greedy-deck-finder.mdc`。
- 对战评估器会缓存 `DeckRep.Signature()` 对应的 `Deck`，避免 BO 与系列赛内重复构建。
- 代表排列阶段由全对全改为淘汰式，比赛数量从近 `O(n^2)` 降低到近 `O(n)`。
- 瑞士轮：若至少 `K*M` 名其他选手的当前瑞士分已严格高于某候选的理论最高终分（当前分 + 剩余轮数，每轮至多 +1），该候选不可能进入瑞士结束后的前 `K*M` 名，移出后续配对；`--perf` 中「瑞士剪枝淘汰」为各 `size` 轮次上该移除人次的累计。
- 瑞士轮每一轮将**所有分数桶**产生的对局合并为**一次** `PlayBoNBatch`，避免按桶小批次调用导致无法并行。
- `BattleEvaluator` 在 `workers>1` 且一批至少 **2** 场对局时对 `PlayBoNBatch` / `PlaySeriesBatch` 走 `Parallel.For`（单对局仍串行，避免调度开销大于收益）。
- 随机数：**统计上可接受即可**，**不强求** 并行路径与串行路径或旧版实现在 **每场对局随机流** 上完全一致；**避免** 为比特级可复现阻碍并行优化（如批内打乱对局顺序以缓解负载不均）。每次进入瑞士轮时从主 `Random` **仅取 1 个** `Next()` 作为瑞士子流种子，瑞士内洗牌/ghost/末位同分乱序只消耗该子流；指定 `--seed` 时，并行批内仍可为对局派生 `Random` 做先后手/平局掷币，且 **批内对局顺序会经随机打乱**，派生种子与槽位对应关系随打乱而变。战斗模拟器内部仍可能使用线程局部随机（如暴击）。未指定 `--seed` 时并行批内仍可用线程局部随机。项目规则：`.cursor/rules/greedy-deck-finder.mdc`。
- `--perf` 分解：`胶水+各批墙钟` 应近似等于 **扩展+瑞士+大循环+加赛** 阶段墙钟之和；胶水为不含 `PlayBoNBatch`/`PlaySeriesBatch` 的纯编排时间。若 **胶水占比** 极低而 **并行 BO 墙钟** 占主导，继续优化并行框架收益有限，宜攻 **单局模拟热点** 或减少对战次数；若 **BO 串行对占比** 或 **串行批墙钟** 偏高，可再考虑合并批次或降低并行阈值。
- **并行性结论**：在大规模搜索（如 `--level 4`、`--workers 8`）下，`单局线程累计/perf墙钟` 已可达 **接近 workers** 的量级，**批内并行利用已较充分**；进一步缩墙钟优先 **模拟器与对战量**，而非重复投入 `Parallel.For`/胶水。详见 [greedy-deck-finder-performance-notes.md §8](./greedy-deck-finder-performance-notes.md) 与 `.cursor/rules/greedy-deck-finder.mdc`。

## 另见

- **[GreedyDeckFinder 性能观察与调优备忘](./greedy-deck-finder-performance-notes.md)**：`--perf`/`[性能·分解]` 解读、胶水与批串行、Amdahl 误判、优化优先级与源码索引（供后续 Agent 接手）。

## 输出

- 控制台输出每个 `size` 的 `TopK`（组合、代表排列、瑞士分、循环赛分）
- 若提供 `--output`，会写入同样信息到文本文件
