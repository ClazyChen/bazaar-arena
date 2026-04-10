# Engine CLI 契约

本文件定义后端调用 C++ 计算层 CLI 的稳定边界（输入/输出 JSON）。

## 命令行

- `bazaararena_cli --input <input.json> --output <output.json>`
- `bazaararena_cli --version`：打印可执行文件身份（含 `mode=simulate+json` 与 `contract=1`），用于确认**不是**其它同名程序。
- `bazaararena_cli --help`：简要用法。

若 HTTP 后端报告「未写出 out.json」且 stdout 出现与 JSON 无关的「物品列表」等，说明磁盘上的 `bazaararena_cli.exe` **不是**本仓库当前源码编出的对战 CLI，请清理后重新 `cmake --build ... --target bazaararena_cli`，并可用 `--version` 自检。

### HTTP `POST /api/simulate`（可选字段）

- `debug_level`：`none` | `summary` | `detailed`（默认 `detailed`，与 Web 动画一致）
- `seed`：整数；**省略时由服务端随机生成**，并在响应根级字段 `usedSeed` 回显（便于与 `summary` 二次请求、本地 CLI 对齐）
- `max_events`：调试事件上限（默认 50000）

同一场对战要对比「详细事件 vs 摘要行」时：先 `detailed` 拿 `usedSeed`，再对**同一卡组、同一 `seed`** 发 `summary` 即可。

### HTTP `GET /api/simulate/repro-job`

查询参数：`deck_id_0`、`deck_id_1`、**`seed`（必填）**、`debug_level`（默认 `detailed`）、`max_events`（可选）。  
返回 `{ "job": { ...与 CLI 输入相同... }, "used_seed", "debug_level" }`，保存 `job` 为 `job.json` 后可在本地执行：

`bazaararena_cli --input job.json --output out.json`

## 输入 JSON（通用字段）

- `schemaVersion`: number
- `jobId`: string（可选）
- `mode`: string（如 `simulate` / `greedy_search`）
- `payload`: object（模式相关参数）

## 输出 JSON（通用字段）

- `schemaVersion`: number
- `jobId`: string（可选，原样回显输入）
- `ok`: boolean
- `error`: string（当 ok=false）
- `result`: object（模式相关结果）

---

## mode=simulate

用于“给定两套卡组进行一次对战模拟”。

### 输入 payload

- `seed`: number（可选；用于随机胜负裁决等 RNG；缺省时由 CLI 生成）
- `allowTie`: boolean（可选；默认 true）
- `debug`: object（可选）
  - `enabled`: boolean（可选；默认 false）
  - `level`: string（可选；`none|summary|detailed`，默认 `none`）
  - `maxEvents`: number（可选；默认 20000；仅当 enabled=true 时生效，用于截断 debug 事件）
- `sides`: array（必需，长度必须为 2）
  - `sideId`: number（可选；默认 0/1；仅用于输出标识）
  - `level`: number（必需；范围 `1..core::HpTable::MaxLevel`）
  - `attrsOverride`: object（可选；覆写 side 初始属性；禁止包含 `itemCount`）
    - 允许字段：`maxHp|hp|shield|burn|poison|regen|resistance|gold|income|id`
    - 约束：
      - `MaxHp >= Hp > 0`
      - `Income >= 7`
      - 其他属性 `>= 0`
  - `items`: array（必需；长度 `0..SideState::MaxItems`）
    - `key`: string（必需；中文显示名，UTF-8；用于 `GetItemByKey`）
    - `tier`: string（可选；`bronze|silver|gold|diamond`，默认 `bronze`）
    - `attrsOverride`: object（可选）
      - 仅允许：`custom_0|custom_1|custom_2|custom_3`（其他键拒绝）

### side 默认初始化规则（无 attrsOverride 时）

- `MaxHp` 与 `Hp`：查表 `core::HpTable::ByLevel[level]`，并令 `MaxHp = Hp = table[level]`
- `Shield/Burn/Poison/Regen/Resistance/Gold`：全部为 `0`
- `Income`：为 `7`
- `ItemCount`：为 `items.size()`（不可覆写）

### 输入示例

```json
{
  "schemaVersion": 1,
  "jobId": "demo-001",
  "mode": "simulate",
  "payload": {
    "seed": 12345,
    "allowTie": true,
    "debug": { "enabled": true, "level": "detailed", "maxEvents": 20000 },
    "sides": [
      {
        "sideId": 0,
        "level": 5,
        "items": [
          { "key": "獠牙", "tier": "bronze" },
          { "key": "獠牙", "tier": "bronze", "attrsOverride": { "custom_0": 1 } }
        ]
      },
      {
        "sideId": 1,
        "level": 5,
        "items": [
          { "key": "獠牙", "tier": "bronze" }
        ]
      }
    ]
  }
}
```

### 输出 result

- `winner`: number（0/1；当 `isDraw=true` 时为 -1）
- `isDraw`: boolean
- `endTimeMs`: number（模拟结束时刻）
- `final`: object
  - `sides`: array（长度 2）
    - `hp/shield/burn/poison/regen/resistance/gold/income/maxHp`: number（最终快照）
- `debug`: object（仅当 `debug.enabled=true` 且 level!=none 时输出）
  - `level`: string
  - `lines`: array（仅当 level=summary；逐行纯文本日志）
  - `events`: array（仅当 level=detailed；JSON 事件流，可能被截断）
  - `truncated`: boolean

### 输出示例

```json
{
  "schemaVersion": 1,
  "jobId": "demo-001",
  "ok": true,
  "error": "",
  "result": {
    "winner": 0,
    "isDraw": false,
    "endTimeMs": 7350,
    "final": {
      "sides": [
        { "maxHp": 1000, "hp": 12, "shield": 0, "burn": 0, "poison": 0, "regen": 0, "resistance": 0, "gold": 0, "income": 7 },
        { "maxHp": 1000, "hp": -3, "shield": 0, "burn": 0, "poison": 0, "regen": 0, "resistance": 0, "gold": 0, "income": 7 }
      ]
    },
    "debug": {
      "level": "detailed",
      "events": [
        { "t": 0, "kind": "battle_start" },
        { "t": 500, "kind": "cast", "side": 0, "itemIndex": 0, "itemKey": "獠牙" }
      ],
      "truncated": false
    }
  }
}
```

