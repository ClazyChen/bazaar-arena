# 变更记录

## 充能/加速/减速/冻结统一目标选择、Haste 替代 Accelerate、GetResolvedValue 合并、暗影斗篷 TargetCondition

- **统一目标选择**：充能、加速、减速、冻结均采用「目标数 + 时间」两属性；目标池**仅限有冷却时间**的物品，再按 `TargetCondition` 过滤，**不放回**随机选取至多 TargetCount 个；触发（如「触发冻结」）按实际目标数入队。默认：减速/冻结 `Condition.DifferentSide`，充能/加速 `Condition.SameSide`。
- **AbilityDefinition.TargetCondition**：可选，多目标效果的目标筛选条件；由模拟器注入到 `IEffectApplyContext.TargetCondition`。克隆能力时一并克隆。
- **Effect.Haste 替代 Effect.Accelerate**：加速统一使用 `Effect.Haste`；从模板读 `Haste`、`HasteTargetCount`（默认 1），调用 `ApplyHaste(hasteMs, count, ctx.TargetCondition)`。**Effect.Accelerate 已删除**。
- **暗影斗篷**：在物品能力上设 `HasteTargetCount = 1`、`TargetCondition = Condition.RightOfSource`，使用 `Effect.Haste`，不再在预定义 Effect 内写死右侧逻辑。
- **ItemTemplate.HasteTargetCount**：新增，默认 1；与 `TargetCondition` 配合用于加速目标选取。
- **GetResolvedValue 合并 GetCasterItemInt**：`IEffectApplyContext.GetResolvedValue(key, applyCritMultiplier = false, int defaultValue = 0)`，缺省时用 defaultValue；**GetCasterItemInt 已移除**。效果内用 `ctx.GetResolvedValue(nameof(ItemTemplate.FreezeTargetCount), defaultValue: 1)` 等取目标数。
- **Condition**：新增 `DifferentSide`（候选与来源异侧）、`RightOfSource`（候选在来源右侧相邻），供目标选择与暗影斗篷使用。
- **文档与规则**：implementation-notes 增加「充能、加速、减速、冻结统一目标选择」、更新冻结/加速小节；item-design.mdc 更新效果列表（Accelerate→Haste）、GetResolvedValue、TargetCondition、HasteTargetCount。

---

## Condition 与 Effect 委托化重构、UseOtherItem 己方约束、暴击按效果显示

### Condition：委托 + ConditionContext，移除 ConditionKind

- **Core/Condition.cs**：`Condition` 改为持有一个 `Func<ConditionContext, bool>?` 委托；新增 `ConditionContext`（CandidateSide/Item、SourceSide/Item、UsedTemplate、CandidateTemplate）。静态工厂：`SameAsSource`、`DifferentFromSource`、`SameSide`、`AdjacentToSource`、`WithTag(tag)`、`And(a, b)`。移除 `ConditionKind` 枚举与 `Tag` 字段。
- **评估**：触发器与光环处构建 `ConditionContext` 后调用 `condition.Evaluate(ctx)`，不再使用 BattleSimulator 内的 TriggerConditionEvaluator/AuraConditionEvaluator switch。

### UseOtherItem 始终叠加「己方其他物品」

- **EnsureTriggerCondition**（ItemDatabase、BattleSimulator）：UseOtherItem 时**始终**先设基础条件 `And(DifferentFromSource, SameSide)`；若能力有显式 Condition（如姜饼人 `WithTag(Tag.Tool)`），则返回 `And(baseSameSideOther, condition)`，避免对方使用工具触发己方能力。

### Effect：委托驱动，移除 EffectKind 与 CustomEffectHandlers

- **移除**：`EffectKind`、`EffectKindKeys`、`EffectKindExtensions`；`BattleSimulator` 内 `GetEffectApplier` 与各 `ApplyXxx` 静态方法；`CustomEffectHandlers.cs`。`EffectDefinition` 移除 `Kind`、`CustomEffectId`。
- **Core**：新增 `IEffectApplyContext`（Value、HasLifeSteal、IsCrit、GetResolvedValue、各类 Apply/Log；后已合并 GetCasterItemInt 入 GetResolvedValue 并增加 TargetCondition）；`EffectDefinition` 增加 `ApplyCritMultiplier`、`Apply` 委托。预定义效果在 **Core/Effect.cs** 内为每条设置 `Apply`，只读效果用 `ctx.GetResolvedValue(nameof(ItemTemplate.XXX), ...)` 取值，仅 WeaponDamageBonus 保留 ValueKey。
- **暴击**：由物品六字段（Damage/Burn/Poison/Heal/Shield/Regen 任一 > 0）与 `eff.ApplyCritMultiplier` 决定是否乘暴击倍率；`TemplateHasAnyCrittableField` + 队列侧 `ability.Effects.Any(e => e.Apply != null && e.ApplyCritMultiplier)` 决定是否掷暴击。

### 暴击日志按效果区分

- **LogEffect** 增加参数 `showCrit`；可暴击效果（Damage/Burn/Poison/Shield/Heal/Regen）传 `showCrit: ctx.IsCrit`，不可暴击效果（Charge/Freeze/Slow/WeaponDamageBonus）传 false。多效果能力（如毒刺）暴击时，仅伤害等可暴击部分显示「（暴击）」，减速等不显示。

### 文档与规则

- **docs/implementation-notes.md**：新增「Condition 与 Effect 委托化重构」；旧「EffectKind 集中映射」「触发器 Condition 自动补全」标为已废弃或收紧为当前约定。
- **.cursor/rules**：item-design.mdc、battle-simulator-ability-queue.mdc、data-and-logging.mdc 更新为委托式 Condition/Effect、UseOtherItem 己方约束、按效果 showCrit。

---

## 魔法字符串消除、EffectKind 简化与暴击伤害

### 魔法字符串消除

- **Tag**（`Core/Tag.cs`）：`Tag.Weapon`、`Tag.Tool`、`Tag.Apparel`、`Tag.Friend`、`Tag.Food` 替代 "武器"/"工具"/"服饰"/"伙伴"/"食物"。Common 与 CustomEffectHandlers 使用 Tag 常量。
- **Trigger**（`Core/Trigger.cs`）：`Trigger.UseItem`、`Trigger.BattleStart` 替代 "使用物品"/"战斗开始"。Common 与 BattleSimulator 使用 Trigger 常量。
- **nameof(ItemTemplate.xxx)**：AuraDefinition 的 AttributeName、FixedValueKey、PercentValueKey 及 Effect 的 ValueKey、BattleSimulator 的 GetInt 键等改用 `nameof(ItemTemplate.CritRatePercent)`、`nameof(ItemTemplate.Custom_0)` 等，避免手写字符串。

### EffectKind 集中映射与策略表

- **EffectKindKeys**：`GetDefaultTemplateKey(EffectKind)`、`GetLogName(EffectKind)` 集中维护 Kind→模板 key 与 Kind→日志名；**EffectKindExtensions** 提供 `kind.GetDefaultTemplateKey()`、`kind.GetLogName()`。
- **IsCrittableEffect**：简化为 `k != EffectKind.Other`。
- **预定义效果**：Effect.Damage、Burn、Poison、Shield、Heal、Regen 仅设 `Kind` 与 `ValueKey = EffectKind.XXX.GetDefaultTemplateKey()`，不再设 ValueResolver。
- **策略表**：BattleSimulator 中 ExecuteOneEffect 的 switch 改为「EffectApplyContext + EffectApplier 委托 + GetEffectApplier(EffectKind)」；每个标准 Kind 对应一个 ApplyXxx(in ctx)，Other 走 CustomEffectHandlers。

### 暴击伤害与利爪、Desc 分行

- **CritDamagePercent**：ItemTemplate 新增属性，默认 200（2 倍暴击）；暴击倍率 = CritDamagePercent/100，作用于伤害/灼烧/剧毒/护盾/治疗/生命再生。**AuraConditionKind.SameAsSource** 支持「仅自身」光环，利爪光环为自身 CritDamagePercent +100%（200→400，4 倍）。
- **暴击伤害着色**：EffectKeywordFormatting 增加「暴击伤害」与伤害同色。
- **Desc 分行**：Tooltip 中 `template.Desc` 按 `;`/`；` 拆成多段，每段单独替换占位符并渲染为一行。

### 文档与规则

- **docs/implementation-notes.md**：新增「避免魔法字符串」「EffectKind 集中映射与策略表」「暴击伤害与描述分行」等节。
- **.cursor/rules**：development-experience.mdc、item-design.mdc、csharp-standards.mdc 补充 Tag/Trigger/nameof、EffectKind、CritDamage、Desc 分行等约定。

---

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
