# Bazaar Arena

本仓库采用 **monorepo** 结构：

- `data/`：物品数据源（YAML）与 schema（仅提交源数据）。
- `tools/`：YAML 校验与代码/数据库生成工具（Python）。
- `engine/`：C++ 计算层（核心库 + CLI / GDF / GDF-PA）。
- `app/`：Web 应用（后端 Flask + 前端 Vue）。
- `docs/`：架构、协议与开发指南。
- `out/`：本地运行产物目录（不入库；仅保留 `.gitkeep`）。

以下命令均在**仓库根目录**执行（除非另有说明）。

## 环境要求

| 组件 | 用途 |
|------|------|
| **CMake 3.20+**、C++20 编译器 | 构建 `engine/` |
| **Python 3** | 数据生成、分析脚本、Web 后端 |
| **Node.js 18+**（可选） | Web 前端开发 |

## 1. 首次准备：从 YAML 生成数据

```bash
pip install -r tools/item_codegen/requirements.txt
python tools/gen_items_cpp.py      # → engine/src/bazaararena/data/items_generated.cpp
python tools/gen_items_sqlite.py   # → app/backend/data/bazaararena.db（Web 用）
```

详见 [`tools/item_codegen/README.md`](tools/item_codegen/README.md)。

## 2. 构建 C++ 引擎

可执行文件**固定输出到仓库根目录 `bin/`**（与后端约定一致）。

**PowerShell / cmd**

```powershell
cmake -S engine -B engine/build -DCMAKE_BUILD_TYPE=Release
cmake --build engine/build --config Release
```

仅构建单个目标：

```powershell
cmake --build engine/build --config Release --target bazaararena_cli
cmake --build engine/build --config Release --target bazaararena_gdf
cmake --build engine/build --config Release --target bazaararena_gdf_pa
```

**bash（Linux / macOS）**

```bash
cmake -S engine -B engine/build -DCMAKE_BUILD_TYPE=Release
cmake --build engine/build --config Release -j
```

产物：

| 可执行文件 | 说明 |
|-----------|------|
| `bin/bazaararena_cli`（`.exe`） | JSON 对战模拟 CLI |
| `bin/bazaararena_gdf`（`.exe`） | Greedy Deck Finder 搜索 |
| `bin/bazaararena_gdf_pa`（`.exe`） | GDF 泛用度 / 聚类分析 |

自检：

```powershell
bin\bazaararena_cli.exe --version    # 应含 mode=simulate+json contract=1
bin\bazaararena_gdf.exe --help
```

## 3. 对战模拟 CLI（`bazaararena_cli`）

输入 / 输出为 JSON job，协议见 [`docs/engine_cli.md`](docs/engine_cli.md)。

**最小示例**

```powershell
bin\bazaararena_cli.exe --input samples\cli\simulate_minimal_input.json --output out.json
```

**带详细事件流**

```powershell
bin\bazaararena_cli.exe --input samples\cli\simulate_minimal_input_detailed.json --output out.json
```

**GDF 结果对照**（验证卡组与引擎一致）

```powershell
bin\bazaararena_cli.exe --input samples\gdf\verify_gdf_side_level2.json --output out.json
```

更多样例见 [`samples/cli/`](samples/cli/)。

## 4. Greedy Deck Finder（`bazaararena_gdf`）

独立可执行文件，**不**走 JSON job 协议。须在仓库根目录运行（或显式指定 `--data-dir` 绝对路径）。完整参数说明见 [`docs/bazaararena_gdf.md`](docs/bazaararena_gdf.md)。

**单锚点搜索（示例：刺刀，6 级）**

```powershell
bin\bazaararena_gdf.exe --data-dir data/items --anchor-item 刺刀 --level 6 --top-k 5 --workers 8 --lambda-anchor 0.5 --mu-diversity 0.1
```

**多种子有序卡组**

```powershell
bin\bazaararena_gdf.exe --data-dir data/items --seed-items 龙涎香,刺刀 --level 8 --output out/gdf_out.txt
```

**Mak 英雄池**

```powershell
bin\bazaararena_gdf.exe --data-dir data/items --pool-hero Mak --anchor-item 魂石 --level 5 --top-k 10
```

**枚举池内每个物品作锚点**（耗时与池大小成正比）

```powershell
bin\bazaararena_gdf.exe --data-dir data/items --enumerate-anchors --level 4 --top-k 3 --workers 4
```

## 5. GDF 分析流水线（枚举 → 泛用度 → Excel）

运行产物写入本地 `out/`（已 gitignore，不提交）。典型流程：先用 Python 脚本批量跑 GDF 并汇总 Top-K，再跑 GDF-PA 生成 `generality.csv` 等，最后导出 Excel。

### 5.1 枚举全部锚点，输出 TSV 与 full Top-K

```powershell
python scripts/gdf_enumerate_anchor_top1.py -o out/gdf_mak_l5.tsv --pool-hero Mak --level 5 --top-k 10
```

默认会额外生成 `out/gdf_mak_l5_full_topk.txt`（与 `-o` 同目录、同名 `_full_topk` 后缀）。常用参数：`--pool-hero`、`--level`、`--top-k`、`--top-multiplier`。

### 5.2 泛用度 / 聚类分析（`bazaararena_gdf_pa`）

```powershell
bin\bazaararena_gdf_pa.exe --data-dir data/items --full-topk-path out/gdf_mak_l5_full_topk.txt --out-dir out/mak/l5 --pool-hero Mak --level 5 --top-k 10
```

输出目录包含 `generality.csv`、`specialty.csv`、`clusters.csv`、`graph_edges.csv` 等。

### 5.3 合并为 Excel

```powershell
pip install openpyxl
python scripts/merge_generality_to_excel.py --out docs/generality_export.xlsx
```

脚本从 `out/vanessa/l*` 与 `out/mak/l*` 下的 `generality.csv` 读取数据（需先对各等级跑完 GDF-PA）。

## 6. Web 应用

前端 + Flask API 的本地开发与部署见 [`app/README.md`](app/README.md)。

**快速启动（两个终端）**

```powershell
# 终端 1：API（端口 5000）
$env:PYTHONPATH = "$PWD\app\backend\src"
pip install -r app/backend/requirements.txt
python -m flask --app bazaararena_api.main:app run --port 5000

# 终端 2：前端（端口 5173）
cd app/frontend
npm install
npm run dev
```

浏览器打开 `http://127.0.0.1:5173`。API 健康检查：`curl http://127.0.0.1:5000/health`。

后端默认调用 `bin/bazaararena_cli` 进行对战模拟；可通过环境变量 `BAZAARARENA_CLI` 覆盖路径。

## 相关文档

| 文档 | 内容 |
|------|------|
| [`docs/engine_cli.md`](docs/engine_cli.md) | `bazaararena_cli` JSON 协议 |
| [`docs/bazaararena_gdf.md`](docs/bazaararena_gdf.md) | GDF 参数、算法与输出 |
| [`docs/architecture.md`](docs/architecture.md) | 整体架构 |
| [`app/README.md`](app/README.md) | Web 前后端 |
| [`tools/item_codegen/README.md`](tools/item_codegen/README.md) | YAML → C++ / SQLite |
