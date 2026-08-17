# 优质阵容探测主管线（人类/AI 操作手册）

> 本文是 Mak 工作收束后的**唯一主管线文档**。遵循本文可在任意 英雄×等级 上产出：
> 收敛认证的精英报告（`docs/meta/<hero>-l<level>.md`）+ 前端卡组集合 + 可复算的原始数据（`out/`）。
> 已完成：Mak L2 / L5 / L8 / L11 / L14 / L17（报告见 `docs/meta/`，对比页 `docs/mak-levels-generalization.md`）。
> 历史路线（GDF/meta 路线之争、理由图提议器）已归档至 `docs/archive/`，仅供了解背景。

## 0. 核心概念（先读）

- **真值层**：`bin/bazaararena_meta`（C++，复用 GDF 物品规则，逐局可复现，~3-9 万局/秒）。
  一切强度结论只能出自它；`bazaararena_cli` 逐局路径仅用于观战/调试（两通道已多次验证逐帧一致）。
- **收敛（converged）**：一套卡组在其单步邻域（单格替换/减件/排列等价类）内无可确认升级。
  **未收敛的卡组没有精英资格**——这是精英定义的第一条。
- **场（field）**：评估一组卡组时使用的对手集合。avg 永远指"对场的平均胜率"，必须注明场。
- **确认制**：任何"升级"必须先在初筛 seed 上达标（Δ≥0.05）、再在**独立 seed 集**上复核达标（Δ≥0.03）。
- **分层报告**：精英层（收敛 + avg≥0.45 或 Nash σ 支撑）/ 流派层（收敛、独立成派但未过线）/ 证伪（不列入）。

## 1. 前置条件

1. `bin/bazaararena_meta.exe` 与 `bin/bazaararena_cli.exe` 为当前源码 Release 构建：
   `cmake --build engine/build --config Release --target bazaararena_meta`（cli 同理）。
2. 物品数据唯一事实来源是 `data/items/*.yaml`；物品变更后必须先走 AGENTS.md 的
   「数据流水线」（codegen → 构建 → SQLite），再跑本管线。**物品变更会使对战缓存失效**
   （缓存按 YAML 内容指纹自动识别，无需手清）。
3. Python 依赖：仅 pyyaml（管线脚本）；评估层无 Python 规则复刻（纪律，见 §4）。

## 2. 主管线（每 英雄×等级 一遍）

| 步骤 | 命令（仓库根） | 耗时 | 产物 |
|---|---|---|---|
| 1. 锚点枚举 | `python scripts/meta_search/enumerate.py --hero Mak --level 8 -o out/meta_search/gdf_mak_l8.tsv --raw-dir out/meta_search/raw_l8 --full-topk-output out/meta_search/gdf_mak_l8_full_topk.txt` | 15–30 min | TSV/full topk |
| 2. 真值矩阵 | `python scripts/meta_search/matrix.py --tsv <tsv> --level 8 --out out/meta_search/matrix_l8.json` | 1–3 min | 全对全自适应矩阵 |
| 3. Nash | `python scripts/meta_search/nash.py --matrix <matrix>` | <1 min | σ 支撑 |
| 4. 挑候选 | 手工（原则见 §2.1）→ `candidates.json` | — | 8–14 套 |
| 5. 精英闭环 | `python scripts/meta_search/elite_report.py --level 8 --decks-file <candidates.json> --out-dir out/elite_report/<tag>` | ~10 min | 收敛场 + 报告骨架 |
| 6. 人工复核 | 引擎标签/重命名/分层终审 → `docs/meta/<hero>-l<level>.md` | 手工 | 正式报告 |
| 7. 前端同步 | `python scripts/import_reference_decks_mak.py --level 8` | 秒级 | 卡组集合 |
| 8. 归档 | 更新对比页与 AGENTS.md 索引 | — | — |

### 2.1 候选挑选原则（步骤 4）

从矩阵读三类成员，凑 8–14 套、覆盖不同引擎核心：

- 矩阵 avg 顶部（注意同壳收敛——同族只取 1–2 个代表，勿全收）；
- Nash σ 支撑成员（即使 avg 不高——针对位）；
- 跨等级/既往精英的收敛形态（对照组，检验等级迁移）。
  候选质量决定闭环视野；漏掉的流派不会自己出现（但三轮玩家实验提示跳跃式盲区接近空集）。

### 2.2 人工复核要点（步骤 6）

- **按最终内容重新命名**（闭环 drift 后锚点名常与阵容脱钩）；
- 逐套标注引擎核心标签，同流派多形态合并叙述；
- 精英层/流派层按 §0 定义划分；被闭环淘汰的形态记入沿革、不进名单；
- 报告模板：场与方法 → 总表（avg/复测/最差 matchup/σ）→ 克制矩阵 → 分层名单 → 克制要点 → 复跑命令。

## 3. 等级特性速查（Mak 实证）

| 等级 | 血/格/档 | 池 | 形态（v4） | 闭环注意 |
|---|---|---|---|---|
| L2 | 400/6/青铜 | 55 | 一王独大（纯策略） | 小场易噪声游移，撞迭代上限就续跑 |
| L5 | 1000/10/白银 | 107 | 双王克制（自毒 avg 王 vs 双射弓 σ=1） | v4 结构剧变，旧精英多跌落 |
| L8 | 2100/10/黄金 | 135 | 摆锤王族 + 冻结控制（σ 4） | 40 局制即够 |
| L11 | 3900/10/钻石 | 136（+采样仪） | 双壳对立（智者杖系 vs 图书馆冻结系） | — |
| L14 | 6600/10/钻石 | 136 | 单家族多 tech（智者杖系 5 席） | 需 100 局制/换 seed 续跑 |
| L17 | 10200/10/钻石 | 136 | **高原型**（干扰系 + 摆锤智者杖） | 点最优不可达，按家族代表报告 |

## 4. 测量纪律（血泪教训，不可妥协）

1. **禁止 Python 复刻物品规则**（quest/档位/overridable）。已有三次漂移事故；
   物品条件转换唯一允许的旧代码是 `gdf_conditions.py`（仅供 CLI 观战/前端导入）。
2. **seed 协议**：系列赛前半不换边、后半换边（纯赛跑对局存在 100% 边偏差，单局胜负不可外推）。
3. **复测**：关键结论必须在第二 seedbase 上复现才可写入报告。
4. **缓存**：`elite_report.py` 的 `battle_cache.jsonl` 带规则指纹；物品或二进制变更自动失效，
   不要复用旧缓存文件到新的 out-dir（指纹不符会安全忽略）。
5. **解释胜率永远带场与局数**：avg 0.75 在 9 卡组场与 136 卡组矩阵是两个世界
   （软口径 vs 精英口径，见 `docs/archive/mak-player-perspective.md` §2）。

## 5. 常见故障

| 症状 | 原因与处置 |
|---|---|
| 闭环多轮不收敛 | 宽平 meta 的赢家诅咒游移：提高 `--max-seeds`、换 `--seed-base` 续跑；仍不收则按高原型报告（L17 先例） |
| `deck build failed: missing prototype` | 物品不在该等级/英雄池（等级门禁；common 池不进 GDF 场） |
| 结果突然全面漂移 | 物品 YAML 变更（查 git）或 bin/ 陈旧（重编译） |
| 前端对局与报告强度不符 | 卡组集合是旧版：重跑 `import_reference_decks_mak.py`（幂等重建） |
| 排列"升级"频繁出现 | 先确认是否真升级（确认制会过滤）；天平类居中约束看 `perm_constraints.py` |

## 6. 产物地图（Mak 归档现状）

| 等级 | 正式报告 | 前端集合 | 原始数据 |
|---|---|---|---|
| L2 | docs/meta/mak-l2.md | Mak L2 精英（收敛认证 · v4） | out/elite_report/mak_l2_v4/ |
| L5 | docs/meta/mak-l5.md | Mak L5 精英（… v4） | out/elite_report/mak_l5_v4/ |
| L8 | docs/meta/mak-l8.md | Mak L8 精英（… v4） | out/elite_report/mak_l8_v4/ |
| L11 | docs/meta/mak-l11.md | Mak L11 精英（… v4） | out/elite_report/mak_l11_v4/ |
| L14 | docs/meta/mak-l14.md | Mak L14 精英（… v4） | out/elite_report/mak_l14_v4/ |
| L17 | docs/meta/mak-l17.md | Mak L17 精英（高原代表 · v4） | out/elite_report/mak_l17_v4/ |

（v3 旧数据保留在 `out/meta_search/*_v3.*`、`out/elite_report/mak_l*/`（无 v4 后缀目录）等历史路径，仅供回溯。）

方法论文档（已入冷路径）：`docs/archive/mak-l8-elite-neighborhood.md`（邻域认证法起源）、
`docs/archive/mak-player-perspective.md`（玩家视角与实用建议）；对比页 `docs/mak-levels-generalization.md` 仍在 docs/。
玩家实验 harness：`out/player_l8/`（probe_v3.py 批量系列赛、watch.py CLI 观战、skill_probe.py 技能模拟）。

## 7. 明确的后续（非阻塞）

- ~~**探测器结构剪裁**~~：**已完成**（遗产线整组迁入 `scripts/meta_search/legacy/`、
  GDF 正名为锚点枚举器、GDF-PA 退出默认构建、bin/ 同步；L8 闭环复跑家族级一致）。
- **Vanessa**：物品版本待更新后再启动；管线同本文（`--hero Vanessa`）。
- 玩家产品化：转型路线图/双口径标注进前端（骨架见 docs/archive/mak-player-perspective.md §4）。
- 数据层保卫：`docs/archive/item-audit-mak-l8.md` §5 的 lint 规则沉淀为 codegen 硬检查（防第四次物品 bug 推翻 meta）。
