# YAML 数据格式（占位）

本文件定义 `data/items/*.yaml` 的字段、枚举与语义，作为“数据层契约”。\n

## 最小字段\n
- `schemaVersion`\n
- `hero`\n
- `items[]`\n
- 每个物品需有 **`Name`**（UTF-8 中文显示名）；代码生成与 `ItemDatabase` 以 **`Name` 字符串**作为索引键（不再使用 `nameEn`）。\n

后续会把旧 C# 模板字段逐步映射到此格式。\n

## 能力与光环中的公式（AST）

- 公式节点为 `{ type: <名称>, params: [ ... ] }`，可省略 `formula::`、`condition::` 以及 `ItemKey` / `SideKey` / `Tag` / `DerivedTag` 等前缀，由代码生成器补全为 C++ 模板实例。
- 无参条件可写字符串简写，例如 `condition: AdjacentToCaster`。
- **触发条件**：未指定 `condition` 时，默认 `Cast` 触发为 `SameAsCaster`，其余触发为 `SameSide`；可用 `ex_condition` 与默认做 `And` 合并。若指定 `condition`，则整段覆盖，不再合并默认，也不再使用 `ex_condition`。
- **目标条件**：未指定 `target_condition` 时，按 `AbilityType` 取基础条件（如 `AddAttribute` 为 `SameSide`，`Slow` 为 `DifferentSide`，纯伤害类为 `Always`），再与 `ex_target_condition` 做 `And`。若指定 `target_condition`，则整段覆盖，不合并基础条件。
- **光环 `Auras[]`**：未写 `condition` 时，生成器默认 `SameAsCaster`（作用于与释放者同一物品）。
- `Cooldown` 在 YAML 中为**秒**；生成代码使用 `N_s` 字面量（毫秒存引擎）。

