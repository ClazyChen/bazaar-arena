# 探测器结构剪裁 — 交接文档（工作单，未开工）

> 目的：把"按最终角色重排代码结构"这项工作完整交接给下一个会话。
> 性质：**结构性剪裁（移动/正名/退出默认构建），不是重新设计，不允许改任何行为语义**。
> 背景：Mak 探测器已收束（见 `docs/deck-search-pipeline.md`），系统的"真形"已在文档层固定，
> 但代码结构仍拖着 GDF/meta 演进史的残骸——新读者会误以为 GDF 是搜索器、
> `meta_search` 一半模块是生产路径。本工作让代码组织与现行角色对齐。

## 1. 现行角色（剪裁的唯一依据）

| 角色 | 组件 | 说明 |
|---|---|---|
| 候选生成器 | `bazaararena_gdf` 二进制 + `enumerate.py` | 只负责锚点枚举产出初始卡组；其内部贪心/beam/RR 评估不被信任但为枚举所需，**不删不重写** |
| 真值测量层 | `bazaararena_meta` 二进制 | 唯一无历史包袱组件，原样保留 |
| 认证闭环 | `neighborhood_scan.py`、`elite_report.py`（v2） | 主驱动：邻域穷举认证 + hill-climb 收敛闭环 |
| 评估与求解 | `battle.py`、`matrix.py`、`nash.py` | — |
| 工具件 | `perm_constraints.py`(+test)、`gdf_conditions.py`、`smoke_test.py` | gdf_conditions 仅限转换用途（CLI 观战/前端导入） |

遗产线（保留但移出生产路径）：`open_do.py`、`double_oracle.py`、`proposer.py`、`reason_graph.py`、
`mine_engines.py`、`mine_engines_test.py`、`reason_token_census.py`、`pair_probe.py`、
`render_reason_graph.py`、`templates.py`。**只移不删**——它是唯一被证实的替代提议器
（v4 的王由其合成），Vanessa 冷启动若锚点覆盖不足需以此为备胎。

## 2. 目标结构

```
scripts/meta_search/            # 探测器包（如认为包名误导，可整体改名 detector/，非必须）
  <主管线模块，见上表>
  legacy/                       # 遗产线整组迁入（互相 import 关系整组保留）
docs/deck-search-pipeline.md    # 操作手册（已存在，剪裁后更新工具路径表）
engine/                         # 构建目标裁剪见 §3 步骤 4
bin/                            # 入库 exe 同步处理（§3 步骤 5）
```

## 3. 执行步骤（按序，每步可独立验收）

1. **建 legacy 子包并整组移动**：`scripts/meta_search/legacy/`，迁入 §1 遗产线 10 个模块；
   修正互相 import（`from meta_search.x` → `from meta_search.legacy.x`）；
   检查全库引用点（`grep -rn "open_do\|proposer\|reason_graph\|mine_engines" --include="*.py"`），
   生产路径上不应再有 import。
2. **正名 GDF**：`docs/bazaararena_gdf.md` 与相关文档中把 `bazaararena_gdf` 的定性改为
   "锚点枚举器（候选生成器）"，删除一切"卡组搜索器/探测器"措辞；`enumerate.py` 注释同步。
3. **退出 GDF-PA**：`engine/CMakeLists.txt` 中 `bazaararena_gdf_pa` 移出默认构建
   （或加 option 默认 OFF）；`gdf_pa/` 源码目录保留。
4. **（可选）ReasonMine 退出 meta 二进制**：`engine/meta/` 的 `--mine-engines` 模式若要移除，
   同步处理 `mine_engines.py` 的调用说明；不做也可，优先级最低。
5. **bin/ 入库 exe 同步**：`bin/` 是入库目录且后端默认从根 `bin/` 调 CLI——
   `bazaararena_gdf_pa.exe` 若退出构建需从仓库删除并说明；**`bazaararena_cli/meta/gdf` 三个 exe 必须随源码重编**，
   否则会制造"同名但非此物"故障源（AGENTS.md 有先例）。
6. **文档同步**：`scripts/meta_search/README.md`（遗产线段落改为指向 legacy/）、
   `docs/deck-search-pipeline.md`（工具速查表路径）、`AGENTS.md`（目录结构表与索引）。

## 4. 验收标准（全部满足才算完成）

1. **行为不变**：任选一等级（建议 L8，成本最低）按 pipeline 手册端到端复跑
   （枚举可跳过，用既有 TSV），闭环结果与 `docs/meta/mak-l8.md` 家族级一致。
2. `python scripts/meta_search/smoke_test.py` 全过。
3. **零误导检查**：让一个不了解历史的读者只读目录结构 + pipeline 手册，
   其对"探测器由什么组成"的回答应为：候选生成器（GDF 枚举）+ 真值层（meta 二进制）+
   认证闭环（elite_report/neighborhood_scan）+ 评估求解（battle/matrix/nash），无第四样。
4. `legacy/` 内模块可 import（`python -c "from meta_search.legacy import open_do"`），
   但主管线模块对其零依赖。

## 5. 红线

- 不改 `engine/src/bazaararena/core/` 任何调度/战斗语义；不改 `Simulator.cpp`、`AbilityQueue.cpp`。
- 不重写任何遗产模块（移动时只允许改 import 路径）。
- 不动 `docs/meta/` 六份报告、`out/` 归档数据、前端数据库与卡组集合。
- 一次做完一次验收：剪裁必须在一个工作单元内完成并验收，**禁止半成品状态交接**
  （两套说法并存比一套陈旧说法更有害）。

## 6. 入口文档

- `docs/deck-search-pipeline.md`：主管线操作手册（先读）。
- `scripts/meta_search/README.md`：模块现状（主管线/遗产线已划分）。
- `AGENTS.md`：仓库约定（bin/ 同步、bin 固定输出、数据流水线）。
