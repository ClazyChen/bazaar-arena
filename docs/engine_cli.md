# Engine CLI 契约（占位）

本文件定义后端调用 C++ 计算层 CLI 的稳定边界（输入/输出 JSON）。

## 命令行

- `bazaararena_cli --input <input.json> --output <output.json>`

## 输入 JSON（建议字段）

- `schemaVersion`: number
- `jobId`: string
- `mode`: string（如 `simulate` / `greedy_search`）
- `payload`: object（模式相关参数）

## 输出 JSON（建议字段）

- `schemaVersion`: number
- `jobId`: string
- `ok`: boolean
- `error`: string（当 ok=false）
- `result`: object（模式相关结果）

