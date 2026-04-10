# Bazaar Arena — Web 程序

本目录包含 **Vue 3 前端**与 **Flask 后端**，通过 **SQLite** 持久化物品目录与卡组数据。物品定义仍以仓库根目录下的 `data/items/*.yaml` 为源，由脚本同步进数据库（详见 `[tools/item_codegen/README.md](../tools/item_codegen/README.md)`）。

## 目录结构


| 路径                                             | 说明                                              |
| ---------------------------------------------- | ----------------------------------------------- |
| `[frontend/](frontend/)`                       | Vue 3 + Vite + TypeScript + Pinia + Vue Router  |
| `[backend/](backend/)`                         | Flask API、`bazaararena_api` 包                   |
| `[backend/data/bazaararena.db](backend/data/)` | 默认 SQLite 数据库（生成后存在；勿提交敏感数据时可自行加入 `.gitignore`） |


仓库根目录下的 `[pictures/webp/](../pictures/webp/)` 供物品图标使用，文件名与物品 `**Name` 字段**一致，扩展名为 `.webp`。

## 环境要求

- **Node.js**（建议 18+）：用于前端依赖与开发服务器  
- **Python 3**：用于后端与 `tools/gen_items_sqlite.py`  
- **PyYAML**：生成数据库时需要（`pip install -r tools/item_codegen/requirements.txt`）

## 首次准备

在**仓库根目录**执行：

1. **生成 / 更新 SQLite**（会创建 `app/backend/data/bazaararena.db`；若库已存在则**只同步 `items` 表**，保留卡组数据）：
  ```bash
   pip install -r tools/item_codegen/requirements.txt
   python tools/gen_items_sqlite.py
  ```
2. **安装后端依赖**：
  ```bash
   pip install -r app/backend/requirements.txt
  ```
3. **安装前端依赖**：
  ```bash
   cd app/frontend
   npm install
  ```

可选：通过环境变量 `**BAZAARARENA_DB**` 指定数据库文件的绝对路径；未设置时使用默认路径 `app/backend/data/bazaararena.db`（相对仓库根目录）。

## 本地开发（推荐）

需要**两个终端**，均可在仓库根目录或对应子目录中工作。

### 1. 启动 API（端口 5000）

**PowerShell**

```powershell
$env:PYTHONPATH = "$PWD\app\backend\src"
python -m flask --app bazaararena_api.main:app run --port 5000
```

**bash**

```bash
PYTHONPATH=app/backend/src python -m flask --app bazaararena_api.main:app run --port 5000
```

自检：`curl http://127.0.0.1:5000/health` 应返回 `{"ok":true}`。

### 2. 启动前端（默认端口 5173）

```bash
cd app/frontend
npm run dev
```

浏览器打开终端里提示的本地地址（一般为 `http://127.0.0.1:5173`）。Vite 已将 `**/api**` 与 `**/static/pictures**` 代理到 `http://127.0.0.1:5000`，因此无需在前端配置 CORS。

## 前端脚本

在 `app/frontend` 下：


| 命令                | 说明                                      |
| ----------------- | --------------------------------------- |
| `npm run dev`     | 开发服务器（热更新）                              |
| `npm run build`   | 类型检查 + 生产构建，输出 `frontend/dist/`         |
| `npm run preview` | 本地预览构建结果（仍默认走开发时代理配置，联调 API 时需自行对齐后端地址） |


## 功能概览（首版）

- **主页**：选择或新建「卡组集」  
- **卡组集页**：左侧卡组列表（拖拽排序、新建/复制/删除/重命名）；右侧卡组编辑（等级与槽位、拖拽组卡、右键循环稀有度、过滤物品池、保存写入数据库）

更细的 **HTTP 接口与静态资源路径**见 `[backend/README.md](backend/README.md)`。

## 生产部署（简要）

1. `npm run build` 得到静态文件 `app/frontend/dist/`。
2. 由 **Nginx** 等托管静态资源，并将 `/api` 与 `/static/pictures` 反代到 Flask；或由 Flask 挂载 `dist` 并继续提供 API（需自行接线，当前仓库未内置单一入口脚本）。
3. 确保服务器上存在有效的 `bazaararena.db`，或通过 `BAZAARARENA_DB` 指向数据文件。

## 相关文档

- 后端启动与接口示例：[backend/README.md](backend/README.md)  
- YAML → C++ / SQLite 生成与校验：[tools/item_codegen/README.md](../tools/item_codegen/README.md)  
- 整体架构：[docs/architecture.md](../docs/architecture.md)

