# AGENTS.md — Bazaar Arena

> 本文件面向 AI 编码代理，概述项目结构、构建/运行方式与开发约定。所有命令默认在**仓库根目录**执行（除非另有说明）。

## 项目概览

Bazaar Arena 是游戏《大巴扎》（The Bazaar，PVP 自走棋）的**模拟对战测试平台与优质阵容搜索工具**。核心能力：

- 给定两套卡组，用 C++ 引擎进行逐帧自动对战模拟（JSON 输入/输出）。
- GDF（Greedy Deck Finder）：**锚点枚举器（候选生成器）**，按锚点枚举产出初始候选卡组；其内部贪心/beam 评估仅服务枚举本身，不构成强度结论。
- GDF-PA：对 GDF 结果做泛用度 / 聚类分析并导出 CSV / Excel。
- Web 应用（Vue + Flask + SQLite）：浏览物品、编辑卡组、发起对战模拟并播放战斗动画。

采用 **monorepo** 结构，分层设计（见 `docs/architecture.md`）：

- **前端层（Vue）**：只做 UI 与交互，不进行计算。
- **后端层（Flask）**：API、SQLite 持久化与展示级聚合；重计算通过子进程调用 `bin/bazaararena_cli`。
- **计算层（C++20）**：对战模拟与阵容搜索，输出 JSON。
- **数据层（YAML）**：人工维护的物品定义源数据（唯一事实来源）。
- **格式转换层（Python）**：从 YAML 生成 C++ 静态数据与 SQLite 展示库。

## 目录结构

| 路径 | 说明 |
|------|------|
| `data/items/*.yaml` | 物品定义源数据（按英雄分文件，如 `vanessa.yaml`、`mak.yaml`）；schema 见 `data/schemas/item.schema.json` |
| `tools/` | `gen_items_cpp.py`（YAML→C++）、`gen_items_sqlite.py`（YAML→SQLite）；实现代码在 `tools/item_codegen/src/` |
| `engine/` | C++ 计算层：核心静态库 `bazaararena_engine` + 3 个可执行程序 |
| `app/backend/` | Flask API（包 `bazaararena_api`，位于 `src/` 下，非打包安装，靠 `PYTHONPATH` 导入） |
| `app/frontend/` | Vue 3 + Vite + TypeScript + Pinia + Vue Router |
| `bin/` | C++ 可执行文件**固定输出目录**（gitignore 不入库；与后端默认调用路径约定一致，源码变更后须重编，见「重要开发约定」） |
| `out/` | 本地运行产物目录（gitignore，仅跟踪 `.gitkeep`） |
| `samples/` | CLI / GDF 输入输出样例（`samples/cli/` 含大量 `repro` 复现 job） |
| `scripts/` | 探测主管线包 `meta_search/`（操作手册 `docs/deck-search-pipeline.md`；遗产线在 `meta_search/legacy/`）+ 前端卡组导入等脚本 |
| `docs/` | 协议与开发指南（见文末索引） |
| `pictures/webp/` | 物品图标，文件名与物品 `Name` 一致（gitignore，不入库） |

### engine/ 内部

- 头文件在 `engine/include/bazaararena/`，实现在 `engine/src/bazaararena/`，按子域分目录：
  - `core/`：对战核心（`Simulator`、`AbilityQueue`、`BattleContext`、`SideState` 等）
  - `data/`：`ItemDatabase` 与生成的 `items_generated.cpp`
  - `formula/`：YAML 公式 AST 对应的 C++ 模板（`Formula`、`Condition`、`Percent`）
  - `io/`：JSON job 协议（`SimulateJob`、`Sink`、`JsonLite` 等）
  - `gdf/`、`gdf_pa/`：锚点枚举与泛用度分析实现（`gdf_pa` 为遗产工具，已退出默认构建）
- 入口：`engine/cli/main.cpp`、`engine/gdf/main.cpp`、`engine/gdf_pa/main.cpp`；构建定义在 `engine/CMakeLists.txt`（CMake 3.20+，C++20，仅依赖 `Threads`）。

## 环境要求

| 组件 | 用途 |
|------|------|
| CMake 3.20+、C++20 编译器 | 构建 `engine/` |
| Python 3 | 数据生成、分析脚本、Flask 后端 |
| Node.js 18+（可选） | Web 前端开发 |

## 数据流水线（修改物品后的标准流程）

物品定义的唯一事实来源是 `data/items/*.yaml`。任何 YAML 物品变更后须按顺序执行：

1. `python tools/gen_items_cpp.py` → 重写 `engine/src/bazaararena/data/items_generated.cpp`（**整文件重生成，加载全部 YAML，非增量**；脚本失败时扩展 `tools/item_codegen/src/` 下的解析/生成代码，**不要手改生成物**——文件头有 `@generated` 标记）。
2. **对照核对**：对变更的每个物品 `Name`，在 `items_generated.cpp` 中找到对应 `GeneratedItem{ .key = "…" }`，核对 `Desc`/占位符/Tier 数值/Abilities/Auras 与 YAML 一致。
3. Release 构建 CLI（见下节），保证 `bin/bazaararena_cli.exe` 最新。
4. `python tools/gen_items_sqlite.py` → 同步 `app/backend/data/bazaararena.db`（**已有库时只 UPSERT `items` 表**，不会清空 `deck_collections`/`decks`/`deck_slots`）。

依赖：`pip install -r tools/item_codegen/requirements.txt`（pyyaml、jsonschema）。

YAML 要点（详见 `docs/data_format.md`）：以 `Name`（UTF-8 中文显示名）作为索引键；公式节点为 `{ type, params }` AST；`Cooldown` 写秒级字面量（如 `6s`），引擎内统一存毫秒；时长类字段（Cooldown/Charge/Haste/Slow/Freeze 等）**必须带时间单位后缀**，看到裸数字通常是漏写 `s` 的笔误。

## 构建

可执行文件**固定输出到仓库根 `bin/`**（`engine/CMakeLists.txt` 中的 `RUNTIME_OUTPUT_DIRECTORY` 约定；后端默认从这里调用）。

```powershell
cmake -S engine -B engine/build -DCMAKE_BUILD_TYPE=Release
cmake --build engine/build --config Release
# 仅构建单个目标：--target bazaararena_cli / bazaararena_gdf / bazaararena_meta
# （bazaararena_gdf_pa 为遗产目标，需先 -DBAZAARARENA_BUILD_GDF_PA=ON 重新配置）
```

产物：

| 可执行文件 | 说明 |
|-----------|------|
| `bin/bazaararena_cli(.exe)` | JSON 对战模拟 CLI（协议见 `docs/engine_cli.md`） |
| `bin/bazaararena_gdf(.exe)` | 锚点枚举器（Greedy Deck Finder，探测管线候选生成器；不走 JSON job 协议，参数见 `docs/bazaararena_gdf.md`） |
| `bin/bazaararena_gdf_pa(.exe)` | GDF 泛用度 / 聚类分析（**遗产工具，已退出默认构建**：需 `-DBAZAARARENA_BUILD_GDF_PA=ON` 重新配置才会编译，源码保留在 `engine/gdf_pa/`） |
| `bin/bazaararena_meta(.exe)` | 批量对战评估（meta_search 管线的 C++ 计算层；任务单 JSON → JSONL，可复现；协议见 `engine/meta/main.cpp` 头注释） |

## 运行

### 对战模拟 CLI

```powershell
bin\bazaararena_cli.exe --input samples\cli\simulate_minimal_input.json --output out.json
```

输入/输出为 JSON job（`mode=simulate`，含 `seed`、`debug.level`（`none`/`summary`/`detailed`）等字段，契约见 `docs/engine_cli.md`）。更多样例（含大量 `repro` 复现输入）见 `samples/cli/`。

### GDF（须在仓库根目录运行，或显式指定 `--data-dir`）

```powershell
bin\bazaararena_gdf.exe --data-dir data/items --anchor-item 刺刀 --level 6 --top-k 5 --workers 8
bin\bazaararena_gdf.exe --data-dir data/items --seed-items 龙涎香,刺刀 --level 8 --output out/gdf_out.txt
bin\bazaararena_gdf.exe --data-dir data/items --pool-hero Mak --anchor-item 魂石 --level 5 --top-k 10
bin\bazaararena_gdf.exe --data-dir data/items --enumerate-anchors --level 4 --top-k 3 --workers 4
```

### 泛用度分析流水线（遗产：基于 GDF-PA，产物写入本地 `out/`，不提交）

> 当前优质阵容探测主管线见 `docs/deck-search-pipeline.md`（`scripts/meta_search/`）。
> 本节的 GDF-PA 流水线为遗产路线：`bazaararena_gdf_pa` 已退出默认构建，
> 需 `cmake -S engine -B engine/build -DBAZAARARENA_BUILD_GDF_PA=ON` 后单独构建该目标。

```powershell
# 1. 枚举全部锚点，输出 TSV 与 full Top-K
python scripts/gdf_enumerate_anchor_top1.py -o out/gdf_mak_l5.tsv --pool-hero Mak --level 5 --top-k 10
# 2. 泛用度/聚类分析（输出 generality.csv、specialty.csv、clusters.csv、graph_edges.csv 等）
bin\bazaararena_gdf_pa.exe --data-dir data/items --full-topk-path out/gdf_mak_l5_full_topk.txt --out-dir out/mak/l5 --pool-hero Mak --level 5 --top-k 10
# 3. 合并为 Excel（读取 out/vanessa/l* 与 out/mak/l* 下的 generality.csv）
pip install openpyxl
python scripts/merge_generality_to_excel.py --out docs/generality_export.xlsx
```

### Web 应用（两个终端）

```powershell
# 终端 1：Flask API（端口 5000）
$env:PYTHONPATH = "$PWD\app\backend\src"
pip install -r app/backend/requirements.txt   # 依赖仅 flask>=3.0
python -m flask --app bazaararena_api.main:app run --port 5000

# 终端 2：前端（端口 5173，Vite 已代理 /api 与 /static/pictures 到 5000）
cd app/frontend
npm install
npm run dev
```

环境变量：

- `BAZAARARENA_CLI`：覆盖后端调用的 CLI 路径（默认仓库根 `bin/bazaararena_cli`）。
- `BAZAARARENA_DB`：指定 SQLite 绝对路径（默认 `app/backend/data/bazaararena.db`）。

后端主要文件：`app/backend/src/bazaararena_api/` 下 `main.py`（路由）、`db.py`（SQLite）、`simulate_run.py`（调用 CLI 子进程）、`deck_rules.py`、`slot_attrs.py`。接口细节见 `app/backend/README.md` 与 `docs/api.md`。

## 测试与自检

项目**没有单元测试框架**（无 pytest / vitest / C++ 测试目标）。验证手段为：

1. **CLI 身份自检**：`bin\bazaararena_cli.exe --version` 输出须含 `mode=simulate+json` 与 `contract=1`；`bin\bazaararena_gdf.exe --help`。
2. **样例对照**：用 `samples/cli/` 下的 job 跑 `bazaararena_cli` 并检查输出 JSON；`samples/gdf/verify_gdf_side_level2.json` 用于验证卡组与引擎一致。
3. **复现 job 回归**：`samples/cli/` 中的 `*-repro-seed*.json` 是历史问题的固定 seed 复现输入，可用于回归验证。
4. **后端健康检查**：`curl http://127.0.0.1:5000/health` 应返回 `{"ok":true}`；物品列表 `GET /api/items`。
5. **前端类型检查**：`cd app/frontend && npm run build`（内含 `vue-tsc -b`）。
6. 调试对战问题时固定 `seed`，分别生成 `summary` 与 `detailed` 两份输出对照（先用 detailed 拿 `usedSeed`，再同 seed 跑 summary）。

## 代码风格

- **缩进**：默认 4 空格（禁止 Tab）；YAML（`*.yml`/`*.yaml`）2 空格；同一文件内不混用（`.editorconfig`）。
- **C++**：`.clang-format`（BasedOnStyle: LLVM，IndentWidth 4，ColumnLimit 100，Attach 大括号，指针左对齐）。
- **通用**：UTF-8、LF 行尾、文件末尾保留换行、去行尾空白（Markdown 除外）。
- 前端构建即类型检查（`vue-tsc -b && vite build`）。

## 重要开发约定（务必遵守）

以下规则对所有改动生效：

1. **`bin/` 同步（cli-bin-sync）**：凡是改动会进入 `bazaararena_cli` 的代码（`engine/cli/`、参与链接的 `engine/src/bazaararena/` 源文件、`engine/CMakeLists.txt`、影响 CLI 行为的接口等），结束任务前**必须**执行一次 Release 构建，保证仓库根 `bin/bazaararena_cli.exe` 与源码一致——Flask 后端直接调用它。

2. **游戏机制变更须先确认（game-mechanics-confirm）**：对战核心语义（帧序、充能/冻结/能力队列扫描顺序、伤害与触发顺序等）属核心设计，**未经用户明确同意不得修改**。尤其 `engine/src/bazaararena/core/Simulator.cpp` 主循环与 `AbilityQueue.cpp` 的调度语义。怀疑机制问题时，先用最小复现 job、`frame_end` 中的 `ChargedTime`/`FreezeRemaining` 对照、summary 等证据定位，再向用户确认。允许在 `debug.level=detailed` 路径上（`Sink` 的 hook）增加**纯观测性**输出。

3. **根因优先（root-cause-first）**：修 bug 前必须用代码/日志/最小复现定位根因，禁止猜测性改动；不确定时先向用户确认假设与期望行为。

4. **生成物不手改**：`engine/src/bazaararena/data/items_generated.cpp` 由 `gen_items_cpp.py` 生成（`@generated`），生成结果不符时改 `tools/item_codegen/src/` 的 codegen 后重跑。

## 部署

- 无内置一键部署脚本。简要流程（见 `app/README.md`）：
  1. `cd app/frontend && npm run build` 得到 `app/frontend/dist/`。
  2. 由 Nginx 托管静态资源并将 `/api`、`/static/pictures` 反代到 Flask（或由 Flask 自行挂载 `dist`，需自行接线）。
  3. 确保服务器上存在有效 `bazaararena.db`（或用 `BAZAARARENA_DB` 指向），并存在可用的 `bazaararena_cli`（或用 `BAZAARARENA_CLI` 指定）。

## 安全注意事项

- **本地数据库与产物不入库**：`app/backend/data/*.db`、`out/**`、`pictures/webp/`、前端 `node_modules`/`dist` 均已 gitignore；请勿提交。
- **文件操作限定在仓库内**：`out/` 为本地运行产物目录；脚本与工具的默认输出均指向仓库内路径。
- Python 依赖仅 `pyyaml`、`jsonschema`、`flask`；安装第三方包（如 `openpyxl`）时注意使用隔离环境。
- 后端通过子进程调用 `bin/` 下的 CLI；`BAZAARARENA_CLI` 指向错误路径是常见故障（症状：API 报「未写出 out.json」且 stdout 出现非 JSON 内容），用 `--version` 自检身份。

## 文档索引

| 文档 | 内容 |
|------|------|
| `README.md` | 仓库总览与常用命令 |
| **`docs/deck-search-pipeline.md`** | **优质阵容探测主管线（唯一操作手册：枚举→矩阵→Nash→精英闭环→报告→前端同步；含测量纪律与故障表）** |
| `docs/engine_cli.md` | `bazaararena_cli` JSON 协议（含 HTTP `/api/simulate` 字段） |
| `docs/bazaararena_gdf.md` | 锚点枚举器（GDF）参数、算法与输出 |
| `docs/architecture.md` | 分层架构 |
| `docs/bazaar-core.md` | 游戏规则与搜索问题形式化描述 |
| `docs/meta/mak-l2.md` / `mak-l5.md` / `mak-l8.md` / `mak-l11.md` / `mak-l14.md` / `mak-l17.md` | 各等级正式精英报告（收敛闭环认证后的最终阵容、分层、克制关系） |
| `docs/meta/vanessa-l2.md` / `vanessa-l5.md` / `vanessa-l8.md` / `vanessa-l11.md` / `vanessa-l14.md` / `vanessa-l17.md` | Vanessa 各等级正式精英报告（同上口径；烙刀双变体探测、伪装局外排除） |
| `docs/mak-levels-generalization.md` | Mak 六等级泛化对比：meta 结构对比与构筑思路等级规律 |
| `docs/vanessa-levels-generalization.md` | Vanessa 六等级泛化对比：meta 结构对比与构筑思路等级规律 |
| `docs/archive/` | 历史与调查记录（GDF/meta 路线、实证研究、理由图、邻域认证起源、玩家视角分析、物品审计、v3 重算记录）——仅供背景参考，主管线以 deck-search-pipeline.md 与 docs/meta/ 为准 |
| `docs/data_format.md` | YAML 数据格式契约 |
| `docs/api.md` | HTTP API |
| `app/README.md`、`app/backend/README.md` | Web 前后端 |
| `tools/item_codegen/README.md` | YAML → C++ / SQLite 生成与增量行为 |
| `scripts/meta_search/README.md` | 测量底座与探测工具链（主管线模块 + 遗产线说明） |
| `samples/cli/README.md` | CLI 样例用法 |
| `.agents/skills/` | 两个工作流技能：`items-yaml-codegen-db-sync`（YAML 变更后全链路同步）、`web-battle-debug-close-loop`（Web 对战问题四阶段闭环调试） |
