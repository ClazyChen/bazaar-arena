# meta_search — 优质阵容搜索的测量底座与探测工具链

> **主管线操作手册：`docs/deck-search-pipeline.md`**（枚举→矩阵→Nash→精英闭环→报告→前端同步）。
> 本 README 是模块级参考。背景：`docs/bazaar-core.md`（问题形式化）；
> 历史路线文档（实证研究、理由图提议器）已移至 `docs/archive/`。
> 本包是「超越贪心」路线图的**阶段 1（测量底座）**产物：先修测量，再改搜索。

## 主管线模块（当前生产路径）

| 模块 | 作用 |
|------|------|
| `enumerate.py` | 并行锚点枚举：每锚点一个 GDF 进程，进程级并行。输出 TSV + full topk + 每锚点原始日志。 |
| `matrix.py` | meta 矩阵构建：卡组集合 → 全对全自适应系列赛 → payoff JSON；支持增量补齐（`--existing`）。 |
| `nash.py` | 二人零和混合博弈近似求解（fictitious play，零依赖）：混合策略、博弈值、exploitability、支撑集。 |
| `neighborhood_scan.py` | 精英邻域穷举认证：单格替换/减件/排列等价类三类单步扰动 × 精英场实测，产出逐格承重/平换/升级认证与克制卡清单；`--level` 参数化，支持 `--decks-file`/`--scan` 自定义场。 |
| `elite_report.py` | **精英认证闭环（v2，主驱动）**：候选场 → 邻域 hill-climb（自适应波次评估 CI≤0.10、升级独立 seed 确认、波次级精确缓存带规则指纹）→ 自洽场循环赛 + Nash → 报告骨架。高等级宽平 meta 下比固定局数旧版快一个数量级；产物经人工复核引擎标签后晋升 `docs/meta/`。 |
| `battle.py` | 对战评估层：**主路径为 C++ 批量端点 `bazaararena_meta` --serve 常驻模式**（`series_batch`/波次 `play_wave`/**自适应波次 `play_adaptive_wave`**）；物品规则全部由 C++ 处理，**Python 不复刻规则**。旧 CLI 逐局路径仅留作调试对照。JSONL 持久缓存。 |
| `gdf_conditions.py` | **遗留**：GDF 等级规则的 Python 复刻（仅供 CLI 观战、前端导入 `import_reference_decks_mak.py` 等转换场景；min_tier 档位偏移已修正）。新代码不应依赖——直接用 `bazaararena_meta` 收展示名。 |
| `perm_constraints.py` | 从物品 YAML AST 提取位置约束（居中奇偶/相邻/单侧成员），把同一 multiset 的排列划分为机制等价类（供 neighborhood_scan / elite_report 的排列扰动）。 |
| `perm_constraints_test.py` | 排列约束验收：天平案例（61-0 基线）。 |
| `smoke_test.py` | 冒烟自检：CLI 可复现性、等级规则覆写值、series/缓存/自适应收敛。**改任何模块后必跑**。 |

## 遗产线模块（`legacy/` 子包——DO/PSRO 与理由图提议器，已被邻域闭环取代为主流程）

> 已整组迁入 `scripts/meta_search/legacy/`，**只移不删**：这是唯一被证实的替代提议器
> （v4 的王由其合成），Vanessa 冷启动若锚点覆盖不足需以此为备胎。主管线模块对其零依赖。

| 模块 | 作用 |
|------|------|
| `legacy/templates.py` | 装配工具：`check_assembly`、`layout`、`FILLERS`。手写引擎模板库已退役。 |
| `legacy/proposer.py` | 候选提议器：`reason_engine_candidates`（挖掘引擎 × 填充）+ `gdf_topk_candidates`。 |
| `legacy/double_oracle.py` | Double Oracle 外环 + 受限博弈模式。 |
| `legacy/open_do.py` | 开放博弈 Double Oracle：自适应波次评估 + 双提议器 + Jaccard 多样性入池的 PSRO 主循环。 |
| `legacy/open_do_validate.py` | 开放运行验收：引擎覆盖、真值支撑重合、σ 支撑 vs 真值精英 CI 系列赛、gain 轨迹。 |
| `legacy/reason_graph.py` | **理由图**：机制画像 + 有向理由边 + 数值核心。**提取规则唯一事实来源**；可导出静态画像 JSON（`reason_profiles.json`）供 C++ 挖掘。 |
| `legacy/mine_engines.py` | 引擎挖掘（Python 参考实现/回退）。**生产路径为 C++ `bazaararena_meta --mine-engines`（`engine/meta/ReasonMine.cpp`）**。 |
| `legacy/mine_engines_test.py` | 覆盖回归测试：10 已知流派必须全覆盖 + 未建模词元不得超快照 + 挖掘有界性。 |
| `legacy/reason_token_census.py` | 词元普查：YAML AST 词汇强制三分类（已建模/豁免/未建模）。 |
| `legacy/pair_probe.py` | 经验配对探测图：全 K² 惰性对照替换的边际胜率实测。 |
| `legacy/render_reason_graph.py` | 生成 `docs/archive/reason-graph-review.md`（玩家视角人工评审文档）。 |

## Python / C++ 职责分界（性能纪律）

**规则：任何 O(对局数) 或 O(闭包数) 的工作都在 C++；Python 只做编排。**

- **C++（`engine/meta/` → `bin/bazaararena_meta`）**：
  全部对战评估（批量模式 / `--serve` 常驻 / 自适应波次内卷）、全部物品规则
  （quest 覆写 / overridable 缩放 / 魂石变体——复用 GDF 规则代码）、
  引擎闭包挖掘（`--mine-engines`，静态画像 JSON → 引擎清单）。
- **Python（`scripts/meta_search/`）**：仅编排——锚点枚举驱动、Nash 求解、矩阵装配、
  邻域认证与精英闭环、分析/报告/测试。
  （理由图提取、候选提议、DO 外环等遗产编排已迁 `legacy/`。）
- **禁止**：Python 循环逐局调进程、Python 端复刻物品规则（两次漂移教训：quest 丢弃、
  档位错位，见 docs/archive/bazaar-meta-evidence.md §0.1/§0.2）。新代码只收展示名，
  对战与规则一律走 `bazaararena_meta`。

## 用法（仓库根目录）

```powershell
# 冒烟自检（改任何模块后必跑）
python scripts/meta_search/smoke_test.py

# 1. 并行枚举锚点（~13 min @ 12 进程，Mak L8）
python scripts/meta_search/enumerate.py --hero Mak --level 8 -o out/meta_search/gdf_mak_l8.tsv --raw-dir out/meta_search/raw

# 2. 构建/增量补齐 meta 矩阵（自适应预算）
python scripts/meta_search/matrix.py --tsv out/meta_search/gdf_mak_l8.tsv --level 8 --out out/meta_search/matrix_l8_v1.json

# 3. 求解近似混合 Nash + exploitability
python scripts/meta_search/nash.py --matrix out/meta_search/matrix_l8_v1.json
```

## 约定

- 缓存与产物默认写入 `out/meta_search/`（gitignored）。
- 评估器只依赖 `bin/bazaararena_cli(.exe)`（须为当前源码 Release 构建，`--version` 自检）。
- Python 依赖仅 `pyyaml`（与 `tools/item_codegen/requirements.txt` 一致；无 scipy，Nash 用 fictitious play）。
