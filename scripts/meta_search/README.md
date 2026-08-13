# meta_search — 优质阵容搜索的测量底座与元博弈工具链

> 背景与设计依据：`docs/bazaar-meta-evidence.md`（实证研究）与 `docs/bazaar-core.md`（问题形式化）。
> 本包是「超越贪心」路线图的**阶段 1（测量底座）**产物：先修测量，再改搜索。

## Python / C++ 职责分界（性能纪律）

**规则：任何 O(对局数) 或 O(闭包数) 的工作都在 C++；Python 只做编排。**

- **C++（`engine/meta/` → `bin/bazaararena_meta`）**：
  全部对战评估（批量模式 / `--serve` 常驻 / 自适应波次内卷）、全部物品规则
  （quest 覆写 / overridable 缩放 / 魂石变体——复用 GDF 规则代码）、
  引擎闭包挖掘（`--mine-engines`，静态画像 JSON → 引擎清单）。
- **Python（`scripts/meta_search/`）**：仅编排——理由图**提取**（YAML AST 的唯一事实来源，
  导出 `reason_profiles.json`）、候选提议（引擎×填充组合）、DO 外环簿记、
  Nash 求解、矩阵装配、分析/报告/测试。
- **禁止**：Python 循环逐局调进程、Python 端复刻物品规则（两次漂移教训：quest 丢弃、
  档位错位，见 docs/bazaar-meta-evidence.md §0.1/§0.2）。新代码只收展示名，
  对战与规则一律走 `bazaararena_meta`。

## 模块

| 模块 | 作用 |
|------|------|
| `gdf_conditions.py` | **遗留**：GDF 等级规则的 Python 复刻（仅供旧 CLI 路径与转换工具；min_tier 档位偏移已修正）。新代码不应依赖——直接用 `bazaararena_meta` 收展示名。 |
| `battle.py` | 对战评估层：**主路径为 C++ 批量端点 `bazaararena_meta` --serve 常驻模式**（`series_batch`/波次 `play_wave`/**自适应波次 `play_adaptive_wave`：整批对局一次提交、C++ 内卷逐波加局至收敛，全矩阵无缓存 1.4min**）；物品规则全部由 C++（GDF 规则代码）处理，**Python 不复刻规则**。旧 CLI 逐局路径仅留作调试对照。JSONL 持久缓存。 |
| `enumerate.py` | 并行锚点枚举：每锚点一个 GDF 进程，进程级并行（GDF 内部对战单线程，`--workers` 为死代码）。输出与原 `gdf_enumerate_anchor_top1.py` 兼容（TSV + full topk + 每锚点原始日志）。 |
| `matrix.py` | meta 矩阵构建：卡组集合 → 全对全自适应系列赛 → payoff JSON；支持增量补齐（`--existing`）。 |
| `nash.py` | 二人零和混合博弈近似求解（fictitious play，零依赖）：混合策略、博弈值、exploitability、支撑集。 |
| `perm_constraints.py` | **阶段 2**：从物品 YAML AST 提取位置约束（居中奇偶/相邻/单侧成员），把同一 multiset 的排列划分为机制等价类，每类只评一个代表。 |
| `perm_constraints_test.py` | 阶段 2 验收：天平案例（61-0 基线）——约束提取、类划分、类内不可区分、类间碾压差。 |
| `templates.py` | 装配工具：`check_assembly`（魂石全家族互斥/无武器/居中）、`layout`（约束布局）、`FILLERS`（elite 填充软先验）。~~手写引擎模板库~~ 已退役（v3 起由挖掘引擎替代）。 |
| `proposer.py` | 候选提议器：`reason_engine_candidates`（挖掘引擎 × 填充，主力）+ `gdf_topk_candidates`（GDF 枚举 topk，辅助）。 |
| `double_oracle.py` | Double Oracle 外环 + 受限博弈模式（矩阵 oracle，零对战成本验证外环机制）。 |
| `open_do.py` | 开放博弈 Double Oracle：自适应波次评估 + 双提议器 + Jaccard 多样性入池的 PSRO 主循环。 |
| `open_do_validate.py` | 开放运行验收：引擎覆盖、真值支撑重合、σ 支撑 vs 真值精英 CI 系列赛、gain 轨迹。 |
| `reason_graph.py` | **理由图**：机制画像（生产/触发消费/资源缩放/选择器/供能）+ 有向理由边 + 数值核心（成长/收益/高基础，频率加权）。**提取规则唯一事实来源**；可导出静态画像 JSON（`reason_profiles.json`）供 C++ 挖掘。 |
| `mine_engines.py` | 引擎挖掘（Python 参考实现/回退）：v1 闭包枚举（无截断完备）；v2 核心播种 + 数值加权 + 两级配额。**生产路径为 C++ `bazaararena_meta --mine-engines`（`engine/meta/ReasonMine.cpp`，~30s vs Python ~15min）**。 |
| `mine_engines_test.py` | 覆盖回归测试：10 已知流派必须全覆盖 + 未建模词元不得超快照 + 挖掘有界性。 |
| `reason_token_census.py` | 词元普查：YAML AST 词汇强制三分类（已建模/豁免/未建模），机制通道不可能静默缺失。 |
| `pair_probe.py` | 经验配对探测图：全 K² 惰性对照替换的边际胜率实测（理由图完备性兜底校准）。 |
| `render_reason_graph.py` | 生成 `docs/reason-graph-review.md`（玩家视角人工评审文档）。 |
| `smoke_test.py` | 冒烟自检：CLI 可复现性、等级规则覆写值、series/缓存/自适应收敛。 |

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
