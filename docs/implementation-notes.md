# 实现笔记与错误经验

本文档记录开发过程中容易踩坑的实现细节与修正经验，便于后续维护与代码审查时对照。

---

## 对战模拟器：能力队列的帧边界

### 规则要求

设计文档规定：每一帧先「对于每个物品，如果满足条件，调用使用物品的触发器」（即本帧施放进入队列），再「按照次序结算**上一帧留下的**能力效果」。因此本帧施放产生的能力应在**下一帧**才被处理。

### 错误实现

在步骤（8）处理能力队列时，若在**处理前**就把 `nextAbilityQueue` 拷贝到 `currentAbilityQueue` 并清空 `nextAbilityQueue`，会导致：

- 步骤（7）本帧刚加入的能力立刻进入 `currentAbilityQueue`；
- 同一帧内就被步骤（8）的循环处理掉，相当于「本帧施放、本帧生效」，与「上一帧留下的能力效果」不符。

### 正确做法

- **步骤（8）**：只遍历并处理**当前帧**的 `currentAbilityQueue`（即上一帧末已经移入的条目）。
- **步骤（8）结算完成后**：再将 `nextAbilityQueue` 整体移入 `currentAbilityQueue`，清空 `nextAbilityQueue`，供下一轮循环使用。

这样步骤（7）本帧加入的条目会留在 `nextAbilityQueue`，直到本帧结束才被移入 `currentAbilityQueue`，在下一帧的步骤（8）才被处理。

### 小结

双队列（current / next）时，「下一帧队列 → 当前帧队列」的移动必须发生在**本帧能力全部处理完毕之后**，不能发生在步骤（8）开头，否则会破坏帧边界语义。

### 常见错误与教训（本轮修正）

1. **castQueue 触发的「使用物品」能力必须下一帧处理**  
   步骤 7 中，施放队列产生的能力（UseItem、UseOtherItem）必须加入 **nextAbilityQueue**，不能加入 currentAbilityQueue。若误入 current，本帧步骤 8 就会处理，变成「本帧施放、本帧生效」，违反「上一帧留下的能力在本帧结算」的约定。

2. **步骤 8 只处理 currentAbilityQueue**  
   步骤 8 遍历、消耗的必须是 **currentAbilityQueue**（拷贝后清空 current，再逐条处理）；未到 250ms 或 PendingCount 未用完的写回 **nextAbilityQueue**。**只有步骤 11** 才能把 nextAbilityQueue 移入 currentAbilityQueue；在步骤 8 开头或循环前做「next → current」都是错误的。

3. **步骤 8 执行过程中引发的新能力**  
   ExecuteOneEffect 执行效果时可能触发新能力（如后续扩展的触发器）。这些新能力入队规则：**仅优先级为 Immediate 的加入 currentAbilityQueue**（本帧继续被步骤 8 处理），**其余一律加入 nextAbilityQueue**。因此 ExecuteOneEffect 需要接收 current/next 两个队列参数，供未来触发器调用使用。

4. **触发器调用方式与 PendingCount 合并**  
   - 所有触发器通过**统一**的 `InvokeTrigger(triggerName, sourceSide, sourceItem, context, ...)` 调用，遍历双方所有物品，用 `TriggerConditionEvaluator` 根据 Condition（SameAsSource / DifferentFromSource / WithTag）判断是否触发；战斗开始也经此入队，不直接执行。详见 **.cursor/rules/battle-simulator-ability-queue.mdc**。  
   - **PendingCount 为通用机制**：新能力入队时，**先查 currentAbilityQueue** 是否存在同 (SideIndex, ItemIndex, AbilityIndex)，有则只加 PendingCount；**再查 nextAbilityQueue**，有则只加 PendingCount；**都没有**才新建条目，且新建时仅 Immediate 入 current，其余入 next。

5. **总结**  
   修改能力队列逻辑时务必对照：cast/使用物品 → 只入 next；步骤 8 只消费 current；next → current 仅步骤 11；新触发能力先合并 PendingCount（current 再 next），再按优先级决定入 current 或 next。详见 **.cursor/rules/battle-simulator-ability-queue.mdc**。

---

## 物品模板：按等级属性与集合表达式

### 设计选择

- **只保留 `_intsByTier`**：扩展属性统一用 `Dictionary<string, List<int>>` 存储；单值（如 `CooldownMs = 2000`）存为长度为 1 的列表，读取时 `list.Count == 1` 则对所有 tier 返回该值。不再维护单独的 `_ints`，避免两套存储与同步问题。
- **IntOrByTier**：在对象初始器中同时支持单值（`Damage = 40`）与按等级列表（`Damage = [25, 35, 45, 55]`）。通过隐式转换（`int` → 单元素列表，`int[]`/`List<int>` → 列表）和属性 setter 写入 `_intsByTier`。
- **Key 不对外暴露**：`KeyDamage`、`KeyCooldownMs` 等改为 `private const`，不提供 GetKey 等 API。调用方（如 BattleItemState、BattleSimulator）直接用字符串字面量 `"Damage"`、`"CooldownMs"` 调用 `GetInt(key, tier)`，简化依赖。
- **未定义即默认值**：不使用 `ContainsKey`；`GetInt(key, tier, defaultValue)` 在 key 不存在时直接返回默认值。效果数值解析时若 `GetInt(key, tier)` 为 0 则用 `eff.Value` 作为 fallback。

### 集合表达式直接赋值（Damage = [25, 35, 45, 55]）

- **问题**：集合表达式 `[25, 35, 45, 55]` 无法直接赋给非 BCL 类型，会报「该类型不可构造」或需写 `(IntOrByTier)(int[])[...]`，不够简洁。
- **做法**：使用 C# 12 的 `[CollectionBuilder]`，让自定义类型成为集合表达式的目标类型：
  1. 在类型上标记 `[CollectionBuilder(typeof(IntOrByTier), "Create")]`。
  2. 实现静态方法 `Create(ReadOnlySpan<int> values)`，在方法内构造并返回该类型。
  3. **实现 `IEnumerable<int>` 并暴露 `GetEnumerator()`**：否则编译器报 CS9188「没有元素类型」。编译器通过枚举器推断集合的元素类型，非泛型自定义类型必须提供此信息。
- **依赖**：`CollectionBuilderAttribute` 在 .NET 9+ 的 `System.Runtime.CompilerServices` 中；若目标框架低于 .NET 9，需自行声明同名 attribute。

### 小结

按等级属性用单字典 + 列表长度区分单值/多值；初始器用 IntOrByTier 统一写法；对外用字符串 key、默认值 0 简化逻辑。需要 `X = [a,b,c]` 时用 CollectionBuilder + IEnumerable 让自定义类型支持集合表达式。

---

## 物品与效果扩展（战斗内修改、自定义效果）

### IntOrByTier.Add(delta) 与战斗内模板

- **IntOrByTier**：增加 `Add(int delta)`，对每个 tier 的值加 delta 并返回新的 IntOrByTier；空列表时返回 `[delta, delta, delta, delta]`。用于「本场战斗内增加某数值」类效果（如举重手套给武器加伤害）。
- **战斗内模板**：`BuildSide` 时每个卡组槽位从 resolver 取 template 后**克隆**一份（新 ItemTemplate + SetIntsByTier），再创建 `BattleItemState(clone, tier)`。因此 `BattleItemState.Template` 为当局专用，可直接修改，例如 `wi.Template.Damage = wi.Template.Damage.Add(value)`，无需额外 DamageBonus 字段。
- **Damage 的 getter**：若需对 Damage 做「整体加 delta」并写回，模板的 Damage 属性 get 需返回**完整按等级列表**（如通过 GetIntOrByTier），否则 get 只返回单值会丢失其他 tier。

### 效果数值与 Apply 委托（当前约定）

- **EffectDefinition**：保留 `ValueKey`、`ValueResolver`、`ResolveValue`；增加 `ApplyCritMultiplier`（默认 true）、`Apply`（`Action<IEffectApplyContext>?`）。只读效果（Damage/Burn 等）在 **Core/Effect.cs** 的 Apply 委托内用 `ctx.GetResolvedValue(nameof(ItemTemplate.XXX), applyCritMultiplier)` 取值；仅需指定数值来源字段的效果（如 WeaponDamageBonus）保留 `ValueKey`，由模拟器解析后填入 `ctx.Value`。
- **自定义/扩展效果**：在 `Core/Effect.cs` 中新增静态 `EffectDefinition` 并设置 `Apply` 委托即可；无需单独 Handler 字典。详见 **.cursor/rules/item-design.mdc**。

---

## 光环（Aura）与属性读取

### 使用时机与集成方式

- **仅战斗内**：光环在「读取属性」时生效；局外/UI（ItemDescHelper、MainWindow）直接调用 `ItemTemplate.GetInt(key, tier)`，不传上下文，不参与光环。
- **集成到 GetInt**：`ItemTemplate.GetInt(key, tier, defaultValue, IAuraContext? context)`：当 `context != null` 时，先取基础值再按公式叠加光环：`最终值 = (基础 + Σ 固定) × (1 + Σ 百分比/100)`，多光环的固定与百分比均为加算；被摧毁的物品不提供光环。
- **IAuraContext**：在 Core 中定义，仅提供 `GetAuraModifiers(attributeName, out fixedSum, out percentSum)`。BattleSimulator 实现为 `BattleAuraContext(side, targetItemIndex)`，在 `GetAuraModifiers` 内遍历己方未摧毁物品的 `Template.Auras`，按条件谓词与属性名累加。

### 光环数据与条件

- **AuraDefinition**（Core）：`AttributeName`、`Condition`（`AuraConditionKind`）、`FixedValueKey`、`PercentValueKey`。条件首期支持 `AdjacentToSource`（`|sourceIndex - targetItemIndex| == 1`）；BuildSide 与 ItemDatabase 克隆模板时对 Auras 做深拷贝（逐个 `new AuraDefinition`）。
- **战斗内读属性**：需要光环时传入 context，例如暴击率：`item.Template.GetInt("CritRatePercent", item.Tier, 0, auraContext)`；模拟器在能力队列处理处按当前 (side, itemIndex) 创建 `BattleAuraContext` 并传入。

### 描述占位符：前缀/后缀

- **语法**：大括号内可为 `{前缀Key后缀}`，前缀/后缀为紧挨 key 两侧的「非字母、非数字、非下划线」字符，key 为字母/数字/下划线（如 `{+Custom_0%}` → key=Custom_0，前缀=+，后缀=%）。
- **替换与样式**：单 tier 替换为 `前缀+数值+后缀`；全 tier 为 `前缀+值1+后缀 » 前缀+值2+后缀 » …`。前缀与后缀随数值一起纳入 valueRanges，加粗与 tier 颜色作用于整段（如 "+3%" 整体高亮）。
- **实现**：`ItemDescHelper` 用正则解析占位符，不再维护固定 Placeholders 列表；`{Cooldown}` 仍映射为 key `CooldownMs` 并按秒格式化。

### 暴击与日志、关键词样式

- **暴击日志**：`IBattleLogSink.OnEffect(..., bool isCrit = false)`；当 `isCrit` 为 true 时，Console/TextBox/File 等 sink 在效果行末尾追加「 （暴击）」。
- **关键词着色**：`EffectKeywordFormatting` 中「伤害」「灼烧」「暴击」「暴击率」「暴击伤害」等仅修改颜色，**不加粗**；加粗仅用于物品名称和嵌入的数值区段（由 ItemDescHelper 的 valueRanges 控制）。

---

## 避免魔法字符串（Tag / Trigger / nameof）

### 设计选择

- **标签**：`Core/Tag.cs` 提供 `Tag.Weapon`、`Tag.Tool`、`Tag.Apparel`、`Tag.Friend`、`Tag.Food`、`Tag.Tech` 等常量，对应「武器」「工具」「服饰」「伙伴」「食物」「科技」。物品的 `Tags = [Tag.Weapon]`、`Tags = [Tag.Friend, Tag.Tech]` 等，条件与效果处用 `Tags.Contains(Tag.Weapon)`，不再手写字符串。
- **触发器**：`Core/Trigger.cs` 提供 `Trigger.UseItem`、`Trigger.BattleStart`。能力定义用 `TriggerName = Trigger.UseItem`；模拟器判断用 `ab.TriggerName == Trigger.BattleStart`、`ab.TriggerName != Trigger.UseItem`。
- **属性名与 key**：AuraDefinition 的 `AttributeName`、`FixedValueKey`、`PercentValueKey` 以及 Effect 的 `ValueKey` 使用 `nameof(ItemTemplate.xxx)`，如 `nameof(ItemTemplate.CritRatePercent)`、`nameof(ItemTemplate.Custom_0)`。BattleSimulator 中 `GetInt(nameof(ItemTemplate.CritDamagePercent), ...)` 等同理，重命名属性时编译期可发现漏改。

### 小结

新增物品或效果时优先用 `Tag.*`、`Trigger.*`、`nameof(ItemTemplate.属性)`，避免散落中英文魔法字符串；仅 ItemTemplate 尚未暴露的属性（如 Shield、Regen）在 EffectKindKeys 等处保留字面量。

---

## EffectKind 集中映射与策略表（已废弃）

本节所述 EffectKind、EffectKindKeys、GetEffectApplier、CustomEffectHandlers 已在此前重构中移除，改为**委托驱动的 Condition 与 Effect**。当前约定见「Condition 与 Effect 委托化重构」一节。

---

## 暴击伤害与描述分行

### 暴击伤害（CritDamagePercent）

- **属性**：ItemTemplate 增加 `CritDamagePercent`，默认 200（表示 2 倍暴击）。暴击时最终倍率 = `CritDamagePercent / 100`（200 → 2x，400 → 4x）。作用于伤害、灼烧、剧毒、护盾、治疗、生命再生等所有可暴击效果。
- **光环**：利爪等「自身暴击伤害 +100%」使用 `AuraConditionKind.SameAsSource`（仅 targetItemIndex == sourceItemIndex），`AttributeName = nameof(ItemTemplate.CritDamagePercent)`，`PercentValueKey = nameof(ItemTemplate.Custom_0)`，Custom_0 = 100；公式 `(基础 + Σ固定) × (1 + Σ百分比/100)` 得 200×2=400 即 4 倍。

### Desc 按分号分两行

- **显示**：物品 Desc 中可用分号（`;` 或 `；`）分段。MainWindow 的卡组内/物品池 Tooltip 对 `template.Desc` 按分号 Split 后，每段 trim 再单独做占位符替换与 BuildLineInlines，每段一个 TextBlock，实现两行或多行显示。

---

## 冰锥与冻结、相关修复与约定总结

本节记录「冰锥」物品加入过程中涉及的实现选择与修复，便于后续扩展类似效果（多目标、持续时间、日志与持久化）时对照。

### 冻结（Freeze）与冰锥物品

- **属性设计**：与充能（Charge）一致，**内部存毫秒**，物品定义用**秒**。`ItemTemplate` 提供 `Freeze`（`IntOrByTier` 毫秒）、`FreezeSeconds`（`SecondsOrByTier`：可赋单值或 `new[] { 3.0, 4.0, 5.0, 6.0 }`，内部转毫秒）、`FreezeTargetCount`（冻结目标数量，可单值或按等级）。
- **SecondsOrByTier**：`Core/ItemTemplate.cs` 中新增结构体，支持从 `double` / `double[]` 隐式转换，提供 `ToFreezeMs()` 与 `FromFirstTierMs(ms)`，供定义时写 `FreezeSeconds = new[] { 3.0, 4.0, 5.0, 6.0 }`，符合「物品定义中时间一律用秒」的约定（见 **.cursor/rules/project-conventions.mdc**）。
- **预定义效果**：`Effect.Freeze` 使用模板的 `Freeze`（毫秒）与 `FreezeTargetCount`。目标选择见下节「充能、加速、减速、冻结统一目标选择」；冻结不可暴击；触发「触发冻结」时按**实际目标数**入队（PendingCount）。
- **BattleItemState**：已有 `FreezeRemainingMs`；`ProcessCooldown` 中 `FreezeRemainingMs > 0` 时不推进冷却；每帧在某一步中减少 `FreezeRemainingMs`（见下）。

### 加速/减速/冻结剩余时间与冷却顺序

- **问题**：若先「减少加速、减速、冻结剩余时间」再「处理冷却」，则冻结最后一帧会少 1 帧：本帧初剩余 50ms 被减为 0，再处理冷却时已不视为冻结，冷却会推进。
- **正确顺序**：**先处理冷却**（冻结状态下不推进），**再**减少加速、减速、冻结剩余时间 50ms。这样冻结最后一帧仍在本帧内阻挡冷却，持续时间足额。设计文档「每一帧的结算顺序」与「帧的结算规则」已同步为：步骤 2 处理冷却，步骤 3 加速/减速/冻结减少。

### 多目标效果与日志

- **extraSuffix**：`IBattleLogSink.OnEffect` 增加可选参数 `string? extraSuffix = null`。冻结等多目标效果在应用时收集目标物品名，拼成 `" →[物品名1] →[物品名2]..."` 传入，各 sink 在数值后追加显示。
- **时间类效果显示秒**：`EffectLogFormat.FormatEffectValue` 对「充能」「冻结」将毫秒格式化为「N 秒」或「N.F 秒」，与物品描述一致。
- **冻结关键词颜色**：`EffectKeywordFormatting` 中「冻结」使用 `Color.FromRgb(63, 200, 247)`。

### JSON 与 default.json

- **UTF-8 不转义**：`DeckManager` 的 `JsonSerializerOptions` 增加 `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`，保存卡组集时中文等字符正常写入，不再输出 `\uXXXX`。
- **default.json 不覆盖**：构建时 `Data/Decks/*.json` 复制到输出目录时**排除 default.json**（csproj 中 `Exclude="..\..\Data\Decks\default.json"`）；应用启动时若 `default.json` **不存在**才 `NewCollection()` 并 `SaveCollection(defaultPath)` 生成空集，避免每次构建或运行覆盖用户测试用文件。

### 触发器统一与 Condition 自动补全（已重构，见下节）

### 充能、加速、减速、冻结统一目标选择

- **统一逻辑**：四类效果均采用「目标数 + 时间」两属性。目标池 = **仅含冷却时间 > 0 且未销毁的物品**，再按能力的 `TargetCondition`（可选）过滤；**不放回**随机选取至多 `TargetCount` 个；若满足条件的物品数不足则全部选中。触发类（如「触发冻结」）按**实际选中的目标数**入队（PendingCount）。
- **默认 Condition**：减速、冻结默认 `Condition.DifferentSide`（敌方）；充能、加速默认 `Condition.SameSide`（己方）。物品/能力可通过 `AbilityDefinition.TargetCondition` 覆盖（如暗影斗篷用 `RightOfSource` 指定「施放者右侧物品」）。
- **实现**：`EffectApplyContextImpl.GetTargetIndices(fromSide, fromSideIndex, targetCount, condition)` 构建候选池并 Fisher-Yates 不放回选取；`ApplyFreeze`/`ApplySlow`/`ApplyCharge`/`ApplyHaste` 均调用该逻辑。`IEffectApplyContext.TargetCondition` 由模拟器从当前能力的 `TargetCondition` 注入；`Effect.Haste` 读 `HasteTargetCount`（默认 1）并传 `ctx.TargetCondition` 给 `ApplyHaste`。

### 小结

新增「持续时间 + 多目标」类效果时：时间在模板中用秒、内部毫秒；多目标可复用 extraSuffix 与 FormatEffectValue；若有「剩余时间」且影响冷却，须保证**先处理冷却、再减少剩余时间**。目标选择统一为「有冷却 + Condition + 不放回」。触发器与条件见下节「Condition 与 Effect 委托化重构」。

---

## 修复（Repair）机制

- **语义**：修复将**已摧毁**的己方物品恢复为未摧毁，并重置其 `CooldownElapsedMs = 0`，使该物品重新进入冷却循环。无时长参数。
- **目标选择**：与充能/加速不同，目标池为**已摧毁**（`it.Destroyed == true`）且满足能力 `TargetCondition` 的物品，**不**要求有冷却时间；默认 `TargetCondition` 为 SameSide。不放回随机选取至多 `RepairTargetCount` 个。
- **实现**：`EffectApplyContextImpl` 内单独实现 `GetRepairTargetIndices(fromSide, fromSideIndex, targetCount, condition)`，池子过滤条件为 `Destroyed`，不检查 `GetCooldownMs()`。`ApplyRepair` 对选中目标执行 `Destroyed = false`、`CooldownElapsedMs = 0`，并记日志「修复」+ 实际目标数 + `→[物品名]`。
- **模板与效果**：`ItemTemplate.RepairTargetCount`（可单值或按等级，默认 1）；`Effect.Repair` 读该值并调用 `ctx.ApplyRepair(count, ctx.TargetCondition)`。占位符 `{RepairTargetCount}` 由 ItemDescHelper 解析。
- **标签**：新增 `Tag.Tech = "科技"`（`Core/Tag.cs`），用于如废品场维修机器人等科技类物品。
- **日志着色**：`EffectKeywordFormatting` 中「修复」使用 rgb(143,252,188)。

---

## Condition 与 Effect 委托化重构

本节记录 Condition / Effect 脱离枚举与 switch、改为委托驱动的重构经验，便于后续扩展触发器条件与效果时对照。

### Condition：委托 + ConditionContext，支持 And 组合

- **移除**：`ConditionKind` 枚举与 `Tag` 字段；原基于 switch 的 `TriggerConditionEvaluator` / `AuraConditionEvaluator`。
- **Core/Condition.cs**：`Condition` 类仅持有一个 `Func<ConditionContext, bool>?` 委托；`ConditionContext` 为只读结构体，含 `CandidateSide`/`CandidateItem`、`SourceSide`/`SourceItem`、`UsedTemplate`、`CandidateTemplate`。评估时调用 `condition.Evaluate(ctx)`。
- **静态工厂**：`SameAsSource`、`DifferentFromSource`、`SameSide`、`AdjacentToSource`、`WithTag(tag)`、`And(a, b)`。`WithTag` 的 tag 由闭包捕获；`And` 组合两个条件，用于「己方其他物品」等语义。
- **克隆**：`Condition.Clone(c)` 在 Core 中提供，复制委托引用，供 BuildSide / ItemDatabase 克隆能力时使用。

### UseOtherItem 默认与显式 Condition：始终叠加己方

- **问题**：若 UseOtherItem 仅在有显式 Condition（如姜饼人 `WithTag(Tag.Tool)`）时直接返回该 Condition，则不会限制「己方」，对方使用工具也会触发己方能力。
- **正确做法**：UseOtherItem **始终**先叠加「己方其他物品」基础条件 `baseSameSideOther = And(DifferentFromSource, SameSide)`。若原 Condition 为 null，则 `return baseSameSideOther`；若非 null，则 `return And(baseSameSideOther, condition)`。这样「使用工具时充能」等显式条件与「仅己方」同时满足。详见 **.cursor/rules/battle-simulator-ability-queue.mdc**。

### Effect：脱离 EffectKind，委托驱动 + IEffectApplyContext

- **移除**：`EffectKind` 枚举、`EffectKindKeys`、`EffectKindExtensions`；`GetEffectApplier(EffectKind)` 与 `CustomEffectHandlers` 字典；`EffectDefinition.Kind`、`CustomEffectId`。
- **Core**：`IEffectApplyContext` 定义 `Value`、`HasLifeSteal`、`IsCrit`、`GetResolvedValue(key, applyCritMultiplier = false, defaultValue = 0)`（已合并原 `GetCasterItemInt`）、`TargetCondition`（当前能力的目标选择条件，用于冻结/减速/充能/加速）以及各类 Apply/Log 方法；`EffectDefinition` 增加 `ApplyCritMultiplier`（默认 true）、`Apply`（`Action<IEffectApplyContext>?`）。预定义效果在 **Core/Effect.cs** 内为每条效果设置 `Apply` 委托；只读效果在委托内用 `ctx.GetResolvedValue(nameof(ItemTemplate.XXX))` 或带 `defaultValue` 取值，不依赖 ValueKey（仅 WeaponDamageBonus 保留 ValueKey 以指定数值来源）。
- **暴击**：是否参与暴击由**物品**六字段（Damage/Burn/Poison/Heal/Shield/Regen 任一 > 0）与 **EffectDefinition.ApplyCritMultiplier** 共同决定；模拟器用 `TemplateHasAnyCrittableField` 判断，不再依赖 EffectKind。
- **BattleSimulator**：实现 `EffectApplyContextImpl : IEffectApplyContext`，在 `ExecuteOneEffect` 中构建上下文（含 `CritMultiplier`），对每条效果若 `eff.Apply != null` 则 `eff.Apply(ctx)`；仅当 `eff.ValueKey != null` 时才解析并填入 `ctx.Value`（供 WeaponDamageBonus 等使用）。

### 暴击日志：按效果区分是否显示「（暴击）」

- **问题**：多效果能力（如毒刺：伤害+减速）暴击时，若整条能力共用 `isCrit`，则不可暴击的减速也会显示「（暴击）」。
- **做法**：`LogEffect(effectName, value, extraSuffix, showCrit)` 增加 `showCrit` 参数；可暴击效果（Damage/Burn/Poison/Shield/Heal/Regen）在委托内传 `showCrit: ctx.IsCrit`，不可暴击效果（Charge/Freeze/Slow/WeaponDamageBonus）传 false 或不传（默认 false）。上下文内部打日志的方法（如 ApplyFreeze/ApplySlow/ChargeCasterItem）固定传 `isCrit: false`。

### 小结

扩展新触发器条件时在 Core 增加 Condition 静态工厂或 `And` 组合；扩展新效果时在 Core/Effect.cs 增加 `EffectDefinition` 并设置 `Apply` 委托与可选 `ApplyCritMultiplier`。UseOtherItem 的 EnsureTriggerCondition 必须始终叠加 SameSide，显式 Condition 只作额外过滤。

---

## 能力优先级与步骤 8 执行顺序

### 问题

同一帧内可能有多条能力入队（如己方使用裂盾刀、对方姜饼人 UseOtherItem 等）。若步骤 8 按入队顺序遍历 `currentAbilityQueue`，则执行顺序取决于 InvokeTrigger 遍历双方物品的顺序，**与能力的 Priority 无关**，会导致例如裂盾（High）晚于护盾（Low）触发。

### 正确做法

- 步骤 8 在**处理能力队列前**，对 `toProcessAbilities`（current 的拷贝）按**能力优先级**排序。
- 排序主键：`AbilityPriority` 枚举值升序（Immediate → Highest → High → Medium → Low → Lowest），数字小的先执行。
- 同优先级时按 (SideIndex, ItemIndex, AbilityIndex) 作为次键，保证顺序稳定、可复现。

这样同一帧内触发的多条能力会严格按「高优先级先于低优先级」执行，与设计意图一致。详见 **.cursor/rules/battle-simulator-ability-queue.mdc**。

---

## 物品类型快照（ItemTypeSnapshot）

### 背景

战斗内会修改物品模板数值（如裂盾刀减少对方护盾物品的 Shield）。若用**当前**模板数值判断「是否为护盾物品」「是否可暴击」，则护盾被减到 0 后会被误判为非护盾/不可暴击。

### 做法

- **Core/ItemTypeSnapshot.cs**：只读结构体，含 `IsDamageItem`、`IsBurnItem`、`IsPoisonItem`、`IsHealItem`、`IsShieldItem`、`IsRegenItem` 及 `HasAnyCrittableField`。
- **导入时生成**：`BuildSide` 在应用 `entry.Overrides` 后、加入 `side.Items` 前，对每个物品调用 `ItemTypeSnapshot.FromTemplate(clone, entry.Tier)` 并赋给 `BattleItemState.TypeSnapshot`。
- **判断时使用快照**：模拟器判断「是否可暴击」改为 `ItemHasAnyCrittableField(item)`，内部用 `item.TypeSnapshot.HasAnyCrittableField`；裂盾等效果在遍历「对方护盾物品」时用 `oppItem.TypeSnapshot.IsShieldItem`，不再读当前 `Template.Shield`。

这样护盾/伤害/灼烧等类型与可暴击性在整场战斗中以导入时快照为准，不受战斗内数值修改影响。

---

## BattleSimulator 文件拆分与代码结构

### 约定

除 **ItemDatabase/CommonSmall.cs**、**CommonMedium.cs** 等以数据定义为主的文件外，单文件不宜过长；可将嵌套类、静态辅助拆到同命名空间下的独立源文件。

### BattleSimulator 已拆分出的文件

| 文件 | 职责 |
|------|------|
| **BattleSideDamage.cs** | 静态方法 `ApplyDamageToSide(BattleSide, int, bool)`，护盾吸收与伤害结算，供模拟器与效果上下文共用。 |
| **EffectApplyContextImpl.cs** | `IEffectApplyContext` 实现，承载效果应用逻辑（伤害/治疗/护盾/冻结/减速/裂盾等）。 |
| **BattleAuraContext.cs** | `IAuraContext` 实现，战斗内光环属性累加。 |
| **TriggerInvokeContext.cs** | 触发器调用上下文（Multicast、UsedTemplate）。 |

以上类型均为 `internal`，仅本程序集使用。主逻辑与帧循环保留在 **BattleSimulator.cs**，便于阅读与遵守 **.cursor/rules/battle-simulator-ability-queue.mdc**。

---

## 伤害报表与效果类型（吸血、护盾降低、伤害提高）

### 问题：吸血伤害未统计

- **现象**：毒刺等带 `LifeSteal` 的物品造成伤害时，`Effect.Damage` 为区分展示会以 **「吸血」** 调用 `LogEffect`（`ctx.LogEffect(ctx.HasLifeSteal ? "吸血" : "伤害", value, ...)`），而 **StatsCollectingSink** 的 `OnEffect` 仅对 `effectKind == "伤害"` 累加 `a.Damage` 与 `AddSide(damage: value)`，导致吸血那一下的数值未进入伤害报表。
- **修复**：在 **StatsCollectingSink** 的 switch 中，将 **「吸血」** 与 **「伤害」** 同等处理：`case "伤害": case "吸血":` 均执行 `a.Damage += value` 与 `AddSide(sideIndex, damage: value)`。吸血与伤害为同一数值，仅展示不同，统计时均计入伤害。

### 定向效果日志格式统一

- **约定**：定向/多目标效果统一为「[物品名] 效果名 数值 →[目标]」形式，便于报表与日志解析一致。
- **护盾降低**：裂盾刀等减少对方护盾物品 Shield 时，日志用 **「护盾降低」**（不再用「裂盾」），`extraSuffix = " →[" + string.Join("、", targetNames) + "]"`。
- **伤害提高**：举重手套、暗影斗篷等增加武器伤害时，日志用 **「伤害提高」**（不再用「武器伤害提升」），由 `AddWeaponDamageBonusToCasterSide` / `AddWeaponDamageBonusToCasterSideItem` 内建 targetNames 并调用 `LogEffect("伤害提高", value, extraSuffix)`。报表统计不依赖 effectKind 为「伤害提高」（该效果不进入 Damage 累加），仅伤害类效果（伤害/吸血）计入 Damage。

### 小结

新增或修改 `effectKind` 时，若该效果**实质为造成伤害**（如吸血），需在 **StatsCollectingSink.OnEffect** 中与「伤害」一并计入 `a.Damage` 与 `AddSide(damage)`；定向效果命名与格式见 **data-and-logging.mdc**。

---

## UseOtherItem 右侧条件与加速效果

本节记录「暗影斗篷」等「使用此物品右侧物品时」触发、以及加速（Haste）效果的实现经验。

### Condition.UsedItemRightOfSource 与 RightOfSource

- **UsedItemRightOfSource**（触发条件）：UseOtherItem 时，仅当**被使用的物品**在**能力持有者**的**右侧**时触发（同侧且 `UsedItemIndex == SourceItemIndex + 1`）。`InvokeTrigger` 约定：`Source*` = 被使用物品，`Candidate*` = 能力持有者；故 `Condition.UsedItemRightOfSource` 为 `CandidateSide == SourceSide && SourceItem == CandidateItem + 1`。
- **RightOfSource**（目标选择条件）：候选在来源同侧且紧贴右侧（`CandidateItem == SourceItem + 1`），用于多目标效果的目标筛选，如「对施放者右侧物品加速」。
- **用途**：暗影斗篷等 `TriggerName = Trigger.UseOtherItem`、`Condition = Condition.UsedItemRightOfSource`，能力内用 `Effect.Haste` 并设 `TargetCondition = Condition.RightOfSource`、`HasteTargetCount = 1`，对右侧有冷却物品施加加速。

### 加速（Haste）与 Effect.Haste

- **与减速对称**：`BattleItemState` 已有 `HasteRemainingMs`；每帧先处理冷却（加速时 `advanceMs *= 2`），再在步骤 3 中 `HasteRemainingMs = Math.Max(0, HasteRemainingMs - FrameMs)`。
- **模板**：`ItemTemplate` 提供 `Haste`（毫秒）、`HasteSeconds`（秒，可单值或按等级）、`HasteTargetCount`（目标数，默认 1）；物品定义用 `HasteSeconds = new[] { 1.0, 2.0, 3.0, 4.0 }`。
- **效果**：**Effect.Haste**（已移除 Effect.Accelerate）从模板读 `Haste`、`HasteTargetCount`（默认 1），调用 `ctx.ApplyHaste(hasteMs, count, ctx.TargetCondition)`；目标由统一逻辑选取（己方有冷却 + 满足能力 `TargetCondition`，默认 SameSide）。暗影斗篷在能力上设 `TargetCondition = Condition.RightOfSource`、`HasteTargetCount = 1`，不在 Effect 内写死。
- **日志与 UI**：`EffectLogFormat` 对「加速」将毫秒格式化为「N 秒」；`EffectKeywordFormatting` 中「加速」颜色 `rgb(0,236,195)`；`ItemDescHelper` 支持 `{HasteSeconds}` → `Haste`。

### 单目标武器伤害提高

- **AddWeaponDamageBonusToCasterSideItem(value, targetItemIndexOnCasterSide)**：仅对己方指定下标物品生效；若该物品带 `Tag.Weapon` 则 `Damage.Add(value)` 并 `LogEffect("伤害提高", value, " →[目标名]")`，非武器则不操作、不记日志。
- **Effect.WeaponDamageBonusToRightItem(ValueKey)**：从 ValueKey 取值，对 `ctx.ItemIndex + 1` 调用上述方法，用于暗影斗篷「若右侧为武器则伤害提高」。
