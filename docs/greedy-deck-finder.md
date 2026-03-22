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
  1. 瑞士轮：`ceil(log2(候选数))` 轮，同分桶随机配对，禁止重复交手；
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
- `--seed <n>`：随机种子，便于复现
- `--workers <n>`：并行执行 BO 对战（0/1 为串行）
- `--perf`：输出阶段耗时、BO 数、单局数与吞吐（含代表排列候选数与代表排列 BO 数）
- `--output <path>`：可选，结果写入文件
- `--exclude-item <物品名[,物品名...]>`：可重复传，用于在生成过程中始终排除指定物品（锚定物品不可排除）

## 性能实现说明

- 启动时会对物品池模板做一次性预扁平化（Bronze 单值化 + half-overrides 预应用），减少单局构建开销。
- 对战评估器会缓存 `DeckRep.Signature()` 对应的 `Deck`，避免 BO 与系列赛内重复构建。
- 代表排列阶段由全对全改为淘汰式，比赛数量从近 `O(n^2)` 降低到近 `O(n)`。

## 输出

- 控制台输出每个 `size` 的 `TopK`（组合、代表排列、瑞士分、循环赛分）
- 若提供 `--output`，会写入同样信息到文本文件
