# 变更记录

## 光环系统与轻步靴、占位符与暴击表现

### 光环（Aura）

- **数据模型**：Core 新增 `AuraDefinition`（AttributeName、Condition、FixedValueKey、PercentValueKey）、`AuraConditionKind`（首期 `AdjacentToSource`）、`IAuraContext`（GetAuraModifiers）。`ItemTemplate.Auras` 改为 `List<AuraDefinition>`。
- **结算集成到 GetInt**：`ItemTemplate.GetInt(key, tier, default, IAuraContext? context)` 在 context 非空时按公式 `(基础 + Σ固定) × (1 + Σ百分比/100)` 叠加己方光环；被摧毁物品不提供光环。BattleSimulator 实现 `BattleAuraContext`，暴击率等战斗内读属性处传入 context。
- **轻步靴**（Agility Boots）：小、铜、服饰；无能力，光环「相邻物品 +3% » +6% » +9% » +12% 暴击率」；Desc 使用 `{+Custom_0%}`，见下。

### 描述占位符与 Tooltip

- **前缀/后缀格式**：支持 `{+Custom_0%}` 等形式；大括号内前缀、后缀为非字母数字下划线字符，与数值一并替换并纳入加粗/tier 着色区段。ItemDescHelper 改为正则动态解析占位符，`{Cooldown}` 仍映射 CooldownMs 并按秒显示。
- **轻步靴 Tooltip**：显示为「相邻物品 +3% » +6% » +9% » +12% 暴击率」，前后缀与数值整体高亮。

### 暴击与日志、关键词样式

- **暴击日志**：OnEffect 增加可选参数 `isCrit`；暴击时各 sink 在效果行末尾追加「 （暴击）」。
- **关键词**：「伤害」「灼烧」「暴击」「暴击率」等仅着色、不加粗；仅物品名称与数值区段加粗（EffectKeywordFormatting 中对应 Bold 均为 false）。

---

## 新物品与效果体系（岩浆核心、驯化蜘蛛、举重手套）

### 新物品与触发器

- **岩浆核心**（铜、小）：每场战斗开始时造成 6 » 9 » 12 » 15 灼烧，优先级 Medium。引入**战斗开始**触发器：第 0 帧收集双方 `TriggerName == "战斗开始"` 的能力，按 Priority 排序后依次执行。
- **驯化蜘蛛**（铜、小）：冷却 6s，造成 1 » 2 » 3 » 4 剧毒；标签「伙伴」。ItemTemplate 增加 `Poison` 属性，`Effect.Poison` 与占位符 `{Poison}` 已支持。
- **举重手套**（铜、小）：冷却 5s，标签「工具」「服饰」；使用后己方所有带「武器」tag 的物品伤害提高 1 » 2 » 3 » 4（限本场战斗），优先级 High。通过**自定义效果**实现。

### 自定义效果（EffectKind.Other）

- **EffectDefinition**：增加 `ValueKey`（如 `"Custom_0"`），未设 ValueResolver 时用 `template.GetInt(ValueKey, tier)` 结算；增加 `ResolveValue(template, tier, defaultKey)` 统一解析顺序。增加 `CustomEffectId`，供模拟器按 ID 派发。
- **CustomEffectHandlers**：自定义效果逻辑集中在 `BattleSimulator/CustomEffectHandlers.cs`，按 `CustomEffectId` 注册 Handler；BattleSimulator 在 Other 分支调用 `CustomEffectHandlers.TryExecute(...)`。
- **WeaponDamageBonus**：对己方带「武器」tag 的物品，将其战斗内克隆模板的 `Damage` 加上数值（`Template.Damage = Template.Damage.Add(value)`）。预定义在 `Effect.cs`：`Effect.WeaponDamageBonus(ValueKey: "Custom_0")`，举重手套写 `Effects = [Effect.WeaponDamageBonus(ValueKey: "Custom_0")]`。
- **Custom_0 与 IntOrByTier**：ItemTemplate 增加 `Custom_0`（IntOrByTier）；`IntOrByTier.Add(delta)` 为每个 tier 加 delta 并返回新值。战斗内直接改克隆模板的 Damage，无需 DamageBonus 等额外字段。

### GUI 与表现

- **Tooltip**：若物品冷却为 0，不显示「冷却时间」行（卡组内与物品池一致）。
- **效果颜色**：灼烧 rgb(255,159,69)；剧毒 rgb(14,190,79)（`EffectKeywordFormatting`）。
- **灼烧衰减**：每 tick 减去当前灼烧的 3%（`RatioUtil.PercentFloor` 至少为 1），灼烧 1 会衰减为 0。

### 规则与文档

- **.cursor/rules/item-design.mdc**：新增物品设计经验规则（触发器、数值、ValueKey、自定义效果、CustomEffectHandlers、Tooltip、灼烧衰减等），供新增物品与效果时参考。

---

## 近期更新（卡组编辑、物品描述、Tooltip、UI 调整）

### 卡组列表与编辑

- **卡组重命名与排序**：卡组列表支持「重命名」「上移」「下移」；操作后自动保存到当前卡组集 JSON。列表顺序与 JSON 中 `decks` 数组一致（DeckManager 维护 `_order`）。
- **移除“删除”区域**：卡组编辑区不再提供单独“删除”区；将物品**拖出卡组区域**（拖到物品池、空白处等）即从卡组移除。编辑区 StackPanel 设 `AllowDrop`，在 `EditorArea_Drop` 中收到卡组内物品时调用 `RemoveSlotRow`。
- **卡组第三行名称居中**：卡组显示区域第三行（物品名称）改为水平居中、多行时 `TextAlignment.Center`，小型/中型/大型物品均跨列居中。

### 物品与数据

- **仅保留 Common 物品**：删除 `ItemDatabase/TestItems.cs`，公共物品仅在 `ItemDatabase/Common.cs` 中定义（如獠牙）；`App` 与 `Program` 仅调用 `Common.RegisterAll`。`Data/Decks/default.json` 改为使用「獠牙」的示例卡组（deck_a / deck_b）。
- **ItemTemplate.Desc 与占位符**：`ItemTemplate` 增加 `Desc` 属性；支持占位符 `{Damage}`、`{Cooldown}`、`{Burn}` 等，替换为模板对应字段（Cooldown 以秒显示）。克隆与 BuildSide 时复制 `Desc`。
- **效果预定义与委托**：`EffectDefinition` 支持 `ValueResolver` 委托；预定义 `Effect.Damage`、`Effect.Burn` 等（见 `Core/Effect.cs`），獠牙可写 `Effects = [Effect.Damage]`。模拟器优先用 ValueResolver 结算数值。

### 悬停说明（Tooltip）

- **三行内容**：卡组内或可加入物品上悬停时显示三行：第一行名称（加粗，卡组内按 tier 着色）；第二行「冷却时间：X 秒」；第三行 `Desc`（占位符替换后，关键词按日志风格着色）。
- **卡组内**：占位符显示**当前 tier** 的数值，数值加粗。
- **物品池**：名称与冷却行为白色加粗；第三行 Desc 中占位符为全 tier「5 » 10 » 15 » 20」并按 tier 着色加粗。
- **等级颜色**：四档改为标准色：Bronze(180,98,65)、Silver(192,192,192)、Gold(255,215,0)、Diamond(0,255,255)。Tooltip 背景 `#35322e`，默认文字白色；内边距缩小以减轻白边。
- **性能**：Tooltip 不再在 MouseEnter 时构建。卡组内使用 `ToolTip.Opened` 时再构建内容；物品池使用 `ToolTipService.ToolTipOpening` 时构建，并按 `itemName` 缓存（`_poolToolTipCache`）。`InitialShowDelay` 设为 400ms，减少快速划过时的卡顿。

### 实现与规则

- **共享关键词着色**：`EffectKeywordFormatting.cs` 提供 `BuildInlines`/`BuildParagraph`，与战斗日志一致的「伤害」「灼烧」等颜色与加粗；`SingleSimulateWindow` 的日志段落改为调用该共享方法。
- **占位符与行构建**：`ItemDescHelper.cs` 提供 `ReplacePlaceholdersSingle`、`ReplacePlaceholdersAllTiers` 及 `BuildLineInlines`/`BuildLineInlinesWithTiers`，供 MainWindow 的 Tooltip 使用。
- **Tier 颜色**：`TierToBrushConverter` 与 MainWindow 内 `TierToBrush` 使用上述标准 RGB；卡组内名称行与 Tooltip 中 tier 着色一致。
