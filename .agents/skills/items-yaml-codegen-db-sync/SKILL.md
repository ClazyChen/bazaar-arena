---
name: items-yaml-codegen-db-sync
description: Derives changed item Names from git diff after data/items YAML changes, runs gen_items_cpp and extends codegen when generation fails, reviews items_generated.cpp against YAML Desc, builds Release bazaararena_cli into bin/, and runs gen_items_sqlite to sync bazaararena.db. Use when editing item YAML, regenerating items_generated.cpp, syncing the SQLite items table, or refreshing bin/bazaararena_cli after item data changes. Notes the rare case of new untracked YAML files.
---

# YAML 编辑后更新物品数据库

## 何时使用

- 在**既有** `data/items/*.yaml` 内增改物品条目，需要生成 C++、重建 CLI、同步 SQLite 时。
- **例外**：仅在用户**明确说明**新建了 YAML 文件时，按下文「新文件」处理；平时默认不出现新文件。

## 与其它文档/规则的关系

- **命令与产物细节**（表结构、SQLite 增量行为）：以 [tools/item_codegen/README.md](../../../tools/item_codegen/README.md) 为准；本 Skill 只保留要执行的命令与核对要点。
- **CLI 与 `bin/` 同步**：遵守 [AGENTS.md · 重要开发约定 1（cli-bin-sync）](../../../AGENTS.md)——凡改动进入 `items_generated.cpp` 或参与链接的生成物，结束前必须 Release 构建 `bazaararena_cli`，保证仓库根 `bin/` 下可执行文件为最新。
- **游戏机制**：本流程主要触及 **数据管线**（`tools/item_codegen/src/emit_cpp.py`、`parse_yaml.py`、`formula_emit.py` 等）与生成数据。若核对后需改 **战斗语义**（如 `engine/src/bazaararena/core/Simulator.cpp`），须遵守 [AGENTS.md · 重要开发约定 2（game-mechanics-confirm）](../../../AGENTS.md)，先与用户确认再改核心循环。

## 步骤 1：用 git 确定变更范围

**默认（最常见）**：变更在**已跟踪**的 `data/items/<hero>.yaml` 内部。

1. 列出变更文件（按需组合）：
   - 工作区：`git diff --name-only -- data/items/`
   - 暂存区：`git diff --cached --name-only -- data/items/`
   - 相对上次提交：`git diff --name-only HEAD -- data/items/`
2. 对每个变更文件查看完整 diff，例如：`git diff -- data/items/SomeHero.yaml`（已暂存则用 `git diff --cached -- …`）。
3. 根据 diff 中的 `Name:` / `items` 列表块，列出**本次新增或修改的物品**（`items[].Name` ↔ 生成代码里 `GeneratedItem` 的 `.key`）。
4. **优先**从 diff 收敛到真正动到的条目；若 diff 过大、整文件格式化或难以切块，退化为「该 YAML 内全部 `Name`」作为待核对超集，并说明原因。
5. **时长字段快速体检**：当 YAML 中出现（或被 `Desc` 引用）时长类字段（如 `Cooldown/Charge/Haste/Slow/Freeze/Reload` 及对应 `*_Remaining` / `*TargetCount` 的时长值），注意它们在模板里通常应写成 `Ns`（例如 `1s`、`2500ms` 由 codegen 处理时会统一为毫秒）。若看到裸数字（如 `Freeze: 1`）且文案写的是“秒”，优先判定为**漏写后缀 `s`** 的笔误并修正。

**例外（少见）**：新建 YAML 且尚未 `git add` 时，`git diff --name-only` **不会**列出未跟踪文件。使用 `git status` 或用户给出的路径。不要默认假设会出现新文件。

路径在命令与文档中统一写 `data/items/`（正斜杠）。

## 步骤 2：生成 C++ 并处理脚本失败

在仓库根目录：

```bash
pip install -r tools/item_codegen/requirements.txt
python tools/gen_items_cpp.py
```

- 成功则写入 [engine/src/bazaararena/data/items_generated.cpp](../../../engine/src/bazaararena/data/items_generated.cpp)（**加载全部** `data/items/*.yaml` 并整体重写，非单文件增量）。
- 失败时：根据报错扩展 [tools/item_codegen/src/emit_cpp.py](../../../tools/item_codegen/src/emit_cpp.py)、[parse_yaml.py](../../../tools/item_codegen/src/parse_yaml.py)、[formula_emit.py](../../../tools/item_codegen/src/formula_emit.py) 等，支持新 YAML 结构；修复后重跑直至成功。

## 步骤 3：核对生成 C++ 与 YAML `Desc` 一致

对步骤 1 列出的每个 `Name`，在 `items_generated.cpp` 中定位 `GeneratedItem{ .key = "…" }` 块，对照：

- YAML `Desc` ↔ `t.desc`（占位符如 `{Damage}`、`{Custom_0}` 与生成字段、能力是否一致）。
- 各 Tier 数值、`Abilities` / `Auras` 与 YAML 是否一致（触发器、`type`、公式树等）。

若生成结果与 YAML 意图不符：**优先修改 codegen**（步骤 2），**不要**手改 `items_generated.cpp`（文件头标明 `@generated`）。

## 步骤 4：Release 构建 CLI

与 `cli-bin-sync` 及 [tools/item_codegen/README.md](../../../tools/item_codegen/README.md) 一致：

```bash
cmake -S engine -B engine/build
cmake --build engine/build --config Release --target bazaararena_cli
```

（若 `engine/build` 已配置，可跳过第一行。）

产物输出到仓库根 `bin/`（Windows：`bin/bazaararena_cli.exe`），见 `engine/CMakeLists.txt`。

建议自检：`bin/bazaararena_cli.exe --version`（或 [docs/engine_cli.md](../../../docs/engine_cli.md) 中的契约说明）。

## 步骤 5：更新 SQLite

```bash
python tools/gen_items_sqlite.py
```

更新 [app/backend/data/bazaararena.db](../../../app/backend/data/bazaararena.db)。已有库时主要同步 `items` 表（UPSERT），**不会**因本脚本清空 `deck_collections` / `decks` / `deck_slots` 等；细节见 `tools/item_codegen/README.md`。

## 可选：正在运行的 Web / 后端

若 Flask 已启动，仅替换磁盘上的 `bazaararena.db` 不一定让进程内数据立刻更新。需要重启后端、环境变量与 Web 侧核对时，参见 [web-battle-debug-close-loop](../web-battle-debug-close-loop/SKILL.md) 中「阶段 4」相关步骤；**不作为本流程的必选步骤**。

## 流程概览

```mermaid
flowchart LR
  editYaml[editYaml]
  gitScope[gitScope]
  genCpp[genCpp]
  reviewCpp[reviewCpp]
  cmakeRelease[cmakeRelease]
  genSqlite[genSqlite]
  doneNode[done]
  editYaml --> gitScope
  gitScope --> genCpp
  genCpp --> reviewCpp
  reviewCpp --> cmakeRelease
  cmakeRelease --> genSqlite
  genSqlite --> doneNode
```
