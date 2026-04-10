# item_codegen

从 `data/items/*.yaml` 生成：

- **C++ 静态数据**：`engine/src/bazaararena/data/items_generated.cpp`（供 `engine/` 编译）
- **SQLite 展示库**：`app/backend/data/bazaararena.db`（供 Flask 读物品与持久化卡组）

依赖：安装 `tools/item_codegen/requirements.txt`（含 `pyyaml`）。

## 生成命令

在项目根目录执行：

```bash
pip install -r tools/item_codegen/requirements.txt
python tools/gen_items_cpp.py
python tools/gen_items_sqlite.py
```

## 校验（建议本地/CI）

1. **YAML → C++**：运行 `python tools/gen_items_cpp.py` 后，在 `engine/build` 中编译引擎，例如：

   ```bash
   cmake --build engine/build --config Debug
   ```

2. **YAML → SQLite**：运行 `python tools/gen_items_sqlite.py` 后，确认 `app/backend/data/bazaararena.db` 存在，且可用 `sqlite3` 查看 `items` / `deck_collections` 等表。

**增量行为**：若 `bazaararena.db` **已存在**，脚本**只同步 `items` 表**（UPSERT YAML 中的物品，并删除既不在 YAML 中、也未被任何卡组槽位引用的物品行），**不会**清空 `deck_collections` / `decks` / `deck_slots`。若数据库文件**尚不存在**，则创建完整表结构并写入物品。
