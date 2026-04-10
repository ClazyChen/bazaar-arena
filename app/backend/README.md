# Bazaar Arena 后端（Flask）

## 准备

1. 生成 SQLite 数据库（含物品表与空卡组表）：

   ```bash
   python tools/gen_items_sqlite.py
   ```

2. 安装依赖：

   ```bash
   pip install -r app/backend/requirements.txt
   ```

## 运行 API

在仓库根目录下，将 `app/backend/src` 加入 `PYTHONPATH`，并启动 Flask（默认端口 5000）：

**PowerShell**

```powershell
$env:PYTHONPATH = "$PWD\app\backend\src"
python -m flask --app bazaararena_api.main:app run --port 5000
```

**bash**

```bash
PYTHONPATH=app/backend/src python -m flask --app bazaararena_api.main:app run --port 5000
```

健康检查：`GET http://127.0.0.1:5000/health`

物品列表：`GET http://127.0.0.1:5000/api/items`

静态图片（WebP）：`GET http://127.0.0.1:5000/static/pictures/webp/<文件名>`（文件名需 URL 编码，与物品 `Name` 对应的 `.webp`）

## 前端联调

在 `app/frontend` 运行 `npm run dev`（Vite 已将 `/api` 与 `/static/pictures` 代理到本服务）。
