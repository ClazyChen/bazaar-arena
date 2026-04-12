# Greedy Deck Finder（`bazaararena_gdf`）使用说明

C++ 版 GDF 是**独立可执行文件**，与 `bazaararena_cli`（JSON 对战 CLI）分离；复用同一套引擎库（`Simulator`、`BuildSideState` 等），**不**通过 JSON job 协议运行。

## 可执行文件位置与构建

- Release 构建 [`engine/CMakeLists.txt`](../engine/CMakeLists.txt) 中的 `bazaararena_gdf` 目标后，产物位于仓库根目录 **`bin/`**：
  - Windows：`bin/bazaararena_gdf.exe`
  - Unix：`bin/bazaararena_gdf`
- 推荐在 `engine/build`（或你的构建目录）执行：

```bash
cmake --build . --config Release --target bazaararena_gdf
```

## 运行前提

1. **物品 YAML**：`--data-dir` 指向包含 `*.yaml` 的目录（默认 `data/items`，通常即仓库下的 [`data/items/`](../data/items/)）。程序会读取每个文件顶层的 **`hero:`** 与各物品条目的 **`Name: "..."`**，建立「物品名 → 所属英雄文件」映射，用于 `--pool-hero` 过滤。
2. **工作目录**：若使用默认 `--data-dir data/items`，请在**仓库根目录**启动，保证相对路径有效；否则传入绝对路径或从任意目录指定正确的 `--data-dir`。
3. **卡组表示**：搜索中的卡组经 `SideSpec` → `BuildSideState` 固化为 `SideState` 并缓存；每局对战将模板 **拷贝** 进 `Simulator`（与批量对战思路一致）。

## 命令行参数

| 参数 | 说明 |
|------|------|
| `--data-dir <path>` | 物品 YAML 目录（默认 `data/items`） |
| `--anchor-item <name>` | 单物品锚点；与 `--seed-items` **二选一**（除非使用 `--enumerate-anchors`） |
| `--seed-items <a,b,c>` | 有序多物品种子卡组；逗号分隔，顺序影响插入扩展 |
| `--enumerate-anchors` | 对池中**每个物品**各跑一遍「单物品锚点」搜索；不可与 `--anchor-item` / `--seed-items` 同时使用 |
| `--level <2-20>` | 玩家等级；影响槽位上限、池子 MinTier 门槛、战斗物品档位（见下「规则」） |
| `--top-k <k>` | 每档最终保留的候选数量（beam 输出大小，默认 10） |
| `--top-multiplier <M>` | 瑞士阶段晋级人数 = `k * M`（默认 3） |
| `--bo <n>` | 循环赛 / 锚点增广中**每对**系列赛的局数，须为**正奇数**（非法或偶数时回退为 5） |
| `--workers <n>` | 对战评估并行线程数；`0` 表示不额外开线程（默认 0） |
| `--seed <int>` | 全局随机种子（可选）；指定后批内对战使用确定性子种子流 |
| `--pool-hero <name>` | 物品池过滤：`Vanessa` \| `Mak` \| `Common` \| `All`（大小写不敏感，默认 `Vanessa`） |
| `--exclude-item <a,b>` | 排除物品；可多次传入或逗号分隔 |
| `--lambda-anchor <x>` | 锚点边际权重 λ；**`0`（默认）** 时不计算成对对照增广系列赛（与纯 RR 排序退化一致） |
| `--mu-diversity <x>` | 多样性惩罚系数 μ；对已与入选集合的 **Jaccard 相似度** 取最大值后从目标中减去 `μ * sim` |
| `--diversity-exclude-seeds` | 计算 Jaccard 时**忽略**种子物品名（更强调非锚扩展部分的差异） |
| `--output <path>` | 将摘要写入文件；不指定则打印到标准输出 |
| `--help` / `-h` | 打印简要用法 |

运行开头会打印 `engine_version=...` 便于确认二进制版本。

## 规则与算法概要（与实现对齐）

### 等级与池子

- **槽位上限**：与 legacy 一致，1→4、2→6、3→8、4+→10 槽（按物品 **Size** 之和）。
- **池子 MinTier**：与 legacy `GreedyLevelRules` 一致（银≥5 级、金≥8、钻≥11 等）。
- **战斗档位**：由 `GdfLevelRules::CombatTier(level)` 决定物品 `tier`（bronze/silver/gold/diamond），经 `BuildSideState` 写入 `SideState`。
- **烙刀 Q1/Q2**：当池为 Vanessa 且池中存在「烙刀」时，会额外加入展示名 `烙刀（Q1）`、`烙刀（Q2）`（映射到同一模板与 `custom_1`）。

### 每档 `size` 流水线

1. 由上一档 Top 候选扩展 +1 size 物品，冲突桶内 **BO 淘汰赛** 决代表。
2. **瑞士轮**：轮数 `ceil(log2(N))`，晋级 `min(N, k*M)`；含剪枝与同分桶内配对逻辑。
3. **循环赛**：全对全系列赛，累计得分为 `round_robin_score`。
4. **锚点边际（仅当 λ > 0）**：对每个无序对 `{i,j}`，在已有 `D_i vs D_j` 外，再跑 `D_i' vs D_j` 与 `D_j' vs D_i`，其中 `D'` 为从代表卡组中**去掉全部种子展示名**后的变种（允许空卡组）。  
   `anchor_margin(i) += pts_i(D_i vs D_j) - pts_i(D_i' vs D_j)`（对称地累加 j 侧）。
5. **最终 TopK 选取**：按  
   `objective = RR + λ·anchor_margin − μ·max_{已选 d} Jaccard(D, d)`  
   **贪心**选满 `k` 名（MMR 式多样性）。
6. **满槽档**：对并列最高 RR 的子集可加赛系列赛（实现与 legacy playoff 思路一致）。

当 `λ = μ = 0` 时，不进行增广系列赛；贪心目标退化为按 RR 分依次取满（等价于按 RR 排序取 TopK，同分辅以瑞士分与随机 tie-break）。

## 输出说明

每完成一档 `size`，会向 stdout 或 `--output` 文件追加类似：

- `[GDF] seeds: ...`：本轮使用的种子列表。
- `[GDF] size=<n> top <m>`：该档保留数量。
- 每行一条候选：`RR=` 循环赛累计分、`anchor_m=` 锚点边际、`Swiss=` 瑞士分、以及 `|` 后的物品签名（有序列表逗号连接）。

## 示例

仓库根目录、已构建 `bin/bazaararena_gdf.exe`：

```bash
# 单锚「刺刀」，6 级，瑞士晋级 5*3=15，最终 5 条，8 线程，启用锚点边际与轻度多样性
bin/bazaararena_gdf.exe --data-dir data/items --anchor-item 刺刀 --level 6 --top-k 5 --workers 8 --lambda-anchor 0.5 --mu-diversity 0.1

# 多种子有序卡组 + 排除物品 + 结果写入文件
bin/bazaararena_gdf.exe --data-dir data/items --seed-items 龙涎香,刺刀 --level 8 --exclude-item 某物 --output gdf_out.txt

# 枚举池内每个物品作为单锚点（运行次数 = 池大小，耗时可观）
bin/bazaararena_gdf.exe --data-dir data/items --enumerate-anchors --level 4 --top-k 3 --workers 4
```

## 与 legacy C# GreedyDeckFinder 的差异（须知）

- **数据入口**：C++ 版从 `data/items/*.yaml` 解析 `hero` 与 `Name`；legacy 使用 .NET 物品库。请保持 YAML 与生成物品一致。
- **模板扁平 / Overridable 插值**：legacy 的 `GreedyPreflattenedResolver` 对模板做了档位合并与 overridable 缩放；当前 C++ 版直接使用 **`CombatTier` 对应档位** 的 `ItemTemplate` 属性。若需与 legacy **逐局数值完全一致**，需要后续单独对齐扁平化规则。
- **后端集成**：当前 GDF **仅命令行**；未接入 `bazaararena_cli` 的 JSON `mode`。Web 若需调用应单独包装进程或后续扩展协议。

## 相关文件

- 入口：[`engine/gdf/main.cpp`](../engine/gdf/main.cpp)
- 搜索核心：[`engine/src/bazaararena/gdf/GreedySearcher.cpp`](../engine/src/bazaararena/gdf/GreedySearcher.cpp)
- 瑞士 / 循环赛 / 选优：[`engine/src/bazaararena/gdf/SwissTournament.cpp`](../engine/src/bazaararena/gdf/SwissTournament.cpp)
- 对战评估：[`engine/src/bazaararena/gdf/BattleEvaluator.cpp`](../engine/src/bazaararena/gdf/BattleEvaluator.cpp)
