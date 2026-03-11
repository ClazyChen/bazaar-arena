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
   步骤 7 中，施放队列产生的能力仅通过一次 `InvokeTrigger(Trigger.UseItem, ...)` 加入 **nextAbilityQueue**，不能加入 currentAbilityQueue。「其他物品使用则触发」类能力用 `Trigger.UseItem` + `Condition = And(DifferentFromSource, SameSide)[ + 额外条件 ]` 表达。若误入 current，本帧步骤 8 就会处理，变成「本帧施放、本帧生效」，违反「上一帧留下的能力在本帧结算」的约定。

2. **步骤 8 只处理 currentAbilityQueue**  
   步骤 8 遍历、消耗的必须是 **currentAbilityQueue**（拷贝后清空 current，再逐条处理）；未到 250ms 或 PendingCount 未用完的写回 **nextAbilityQueue**。**只有步骤 11** 才能把 nextAbilityQueue 移入 currentAbilityQueue；在步骤 8 开头或循环前做「next → current」都是错误的。

3. **步骤 8 执行过程中引发的新能力**  
   ExecuteOneEffect 执行效果时可能触发新能力（如后续扩展的触发器）。这些新能力入队规则：**仅优先级为 Immediate 的加入 currentAbilityQueue**（本帧继续被步骤 8 处理），**其余一律加入 nextAbilityQueue**。因此 ExecuteOneEffect 需要接收 current/next 两个队列参数，供未来触发器调用使用。

4. **触发器调用方式与 PendingCount 合并**  
   - 所有触发器通过**统一**的 `InvokeTrigger(triggerName, causeItem?, context, ...)` 调用（causeItem 为引起触发的物品引用，BattleStart 传 null），遍历双方所有物品，构建 `ConditionContext`（Source=能力持有者、Item=causeItem）后调用 `condition.Evaluate(ctx)`；若能力有 `InvokeTargetCondition` 且 context 提供 `InvokeTargetItem`，则再以该目标为 Item 求值。详见 **.cursor/rules/battle-simulator-ability-queue.mdc**。  
   - **PendingCount 为通用机制**：新能力入队时，**先查 currentAbilityQueue** 是否存在同 (Owner, AbilityIndex)（引用相等），有则只加 PendingCount；**再查 nextAbilityQueue** 同上；**都没有**才新建条目（AbilityQueueEntry 持有多元组 Owner=BattleItemState、AbilityIndex、PendingCount、LastTriggerMs），且新建时仅 Immediate 入 current，其余入 next。

5. **总结**  
   修改能力队列逻辑时务必对照：cast/使用物品 → 只入 next；步骤 8 只消费 current；next → current 仅步骤 11；新触发能力先合并 PendingCount（current 再 next），再按优先级决定入 current 或 next。详见 **.cursor/rules/battle-simulator-ability-queue.mdc**。

---

## AbilityDefinition 条件统一化与 UseOtherItem 移除

### 三种条件的语义

- **condition**：引起触发的物品（ConditionContext.Item，如「被使用的物品」）需满足的条件。评估时 Source=能力持有者、Item=引起触发的物品。默认：UseItem → SameAsSource，其他触发器（Freeze/Slow/Crit/Destroy/BattleStart）→ SameSide。
- **InvokeTargetCondition**：触发器所指向的物品需满足的条件（如 Slow 时「被减速的物品」、Freeze 时「被冻结的物品」）。在 `InvokeTrigger` 中，当 context 提供 InvokeTarget（Slow/Freeze 按每个目标调用一次）时，以该目标为 Candidate 求值，不通过则不入队。默认 null 表示不限制。
- **TargetCondition**：能力效果选目标时，目标需满足的条件（充能/加速/减速/冻结/修复等）。效果执行阶段使用，逻辑不变。

### 移除 Trigger.UseOtherItem

「使用其他物品时触发」等价于「使用物品」且 condition 限制为「己方、且非来源物品」：`Condition.And(Condition.DifferentFromSource, Condition.SameSide)`，再与显式条件（如 WithTag(Tag.Tool)、LeftOfSource）取与。因此删除 `Trigger.UseOtherItem`，步骤 7 仅调用一次 `InvokeTrigger(Trigger.UseItem, ...)`；原 UseOtherItem 能力改为 `Trigger.UseItem` + 上述 condition。物品迁移示例：神经毒素、断裂镣铐、姜饼人、暗影斗篷。

### Ability 工厂参数

**Core/Ability.cs** 的工厂（Damage、Shield、Heal、Burn、Poison、Haste、Slow、Freeze）支持可选参数：**condition**（覆盖触发器默认）、**additionalCondition**（仅作参数，在工厂内与默认 condition 做 And 后写入 `AbilityDefinition.Condition`，不单独存到定义）、**invokeTargetCondition**（写入 `InvokeTargetCondition`）。默认 condition 按 trigger：UseItem → SameAsSource，其他 → SameSide。克隆时 `EnsureTriggerCondition(triggerName, ability.Condition)` 仅做「condition ?? default」。

### Slow/Freeze 与 InvokeTargetCondition

`OnFreezeApplied` / `OnSlowApplied` 仍传递目标列表 `(sideIndex, itemIndex)[]`（EffectApplyContextImpl 内部构造），模拟器对每个目标解析为 `BattleItemState` 后调用一次 `InvokeTrigger`，context 带 `InvokeTargetItem`、`Multicast = 1`；`AddOrMergeAbility` 按 (Owner, AbilityIndex) 合并 PendingCount。若能力有 `InvokeTargetCondition`，则以 `InvokeTargetItem` 为 Item 求值，不通过则不入队。

### 小结

修改能力或触发器逻辑时：condition 管「谁触发」，InvokeTargetCondition 管「触发器目标是否满足」（仅 Slow/Freeze 等有目标时），TargetCondition 管「效果选谁」。不再使用 UseOtherItem；「其他物品使用则触发」一律用 UseItem + condition。

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

## 能力与 Effect 合并重构

- **目标**：一个能力 = 一条效果语义；原 `AbilityDefinition.Effects`（`List<EffectDefinition>`）与 `EffectDefinition` 合并进 `AbilityDefinition`。
- **AbilityDefinition**：直接承载 `Value`、`ValueKey`、`ApplyCritMultiplier`、`Apply`（`Action<IEffectApplyContext>?`）、`ResolveValue(template, tier, defaultKey)`；**不再有** `Effects` 列表；**已删除** `EffectDefinition` 类。
- **Core/Effect.cs**：仅保留静态 **Apply 委托**（如 `DamageApply`、`ShieldApply`、`AddAttributeApply(attributeName)`、`ReduceAttributeApply(attributeName)`）供能力引用；预定义效果在委托内用 `ctx.GetResolvedValue(...)` 或 `ctx.Value`、`ctx.TargetCondition` 取值，目标条件由能力 `TargetCondition` 经模拟器注入 `ctx.TargetCondition`。
- **执行**：`BattleSimulator.ExecuteOneEffect` 对**单个能力**解析一次 value、构建一次 context、调用一次 `ability.Apply(ctx)`；暴击判定用 `ability.Apply != null && ability.ApplyCritMultiplier`。
- **克隆**：ItemDatabase / BattleSimulator 克隆能力时复制 `Value`、`ValueKey`、`ApplyCritMultiplier`、`Apply`，无 Effects。
- **多效果能力**：若需「一个触发下先 A 再 B」（如原 Haste + AddAttribute），拆成**两条能力**放入同一物品的 `Abilities` 列表，同优先级时按 AbilityIndex 顺序执行。

---

## 物品与效果扩展（战斗内修改、自定义效果）

### IntOrByTier.Add(delta) 与战斗内模板

- **IntOrByTier**：增加 `Add(int delta)`，对每个 tier 的值加 delta 并返回新的 IntOrByTier；空列表时返回 `[delta, delta, delta, delta]`。用于「本场战斗内增加某数值」类效果（如举重手套给武器加伤害）。
- **战斗内模板**：`BuildSide` 时每个卡组槽位从 resolver 取 template 后**克隆**一份（新 ItemTemplate + SetIntsByTier），再创建 `BattleItemState(clone, tier)`。因此 `BattleItemState.Template` 为当局专用，可直接修改，例如 `wi.Template.Damage = wi.Template.Damage.Add(value)`，无需额外 DamageBonus 字段。
- **Damage 的 getter**：若需对 Damage 做「整体加 delta」并写回，模板的 Damage 属性 get 需返回**完整按等级列表**（如通过 GetIntOrByTier），否则 get 只返回单值会丢失其他 tier。

### 效果数值与 Apply 委托（当前约定）

- **AbilityDefinition**：持有一条效果的 `Value`、`ValueKey`、`ApplyCritMultiplier`、`Apply`（`Action<IEffectApplyContext>?`）、`ResolveValue`。只读效果（Damage/Burn 等）在 **Core/Effect.cs** 的 *Apply 委托内用 `ctx.GetResolvedValue(nameof(ItemTemplate.XXX), applyCritMultiplier)` 取值；指定了 `ValueKey` 的能力（如 AddAttribute、ReduceAttribute）由模拟器解析后填入 `ctx.Value`。目标条件由能力的 `TargetCondition` 注入 `ctx.TargetCondition`。
- **自定义/扩展效果**：在 `Core/Effect.cs` 中新增静态 *Apply 委托（或工厂方法返回委托），在物品定义或 **Ability** 工厂中设置 `AbilityDefinition.Apply` 即可；详见 **.cursor/rules/item-design.mdc**。

### AddAttribute / ReduceAttribute 与统一属性增减

- **统一入口**：「给己方某类物品加属性」「给敌方某类物品减属性」用 **Ability.AddAttribute** / **Ability.ReduceAttribute**（内部使用 `Effect.AddAttributeApply` / `Effect.ReduceAttributeApply`）。
- **目标条件与 Haste/Slow 一致**：目标条件写在能力的 **TargetCondition** 上，由模拟器注入 `ctx.TargetCondition`；Apply 委托内用 `ctx.TargetCondition ?? Condition.SameSide`（AddAttribute）或 `?? Condition.DifferentSide`（ReduceAttribute）。**默认**：AddAttribute 己方(SameSide)、ReduceAttribute 敌方(DifferentSide)。
- **Ability.AddAttribute(attributeName, amountKey?, targetCondition?, additionalTargetCondition?, ...)**：`targetCondition` 非空时**完全代替**默认目标；`additionalTargetCondition` 非空且在未传 targetCondition 时在默认 **SameSide** 上追加，即 `TargetCondition = SameSide & additionalTargetCondition`。例如举重手套、裂盾刀用 `additionalTargetCondition: Condition.WithTag(Tag.Weapon)` / `Condition.WithTag(Tag.Shield)`。
- **Ability.ReduceAttribute(...)**：同上，默认目标为 **DifferentSide**，`additionalTargetCondition` 在 DifferentSide 上追加。
- **简化写法**：仅需「己方全体」或「敌方全体」可不传 target 参数；需收窄目标时用 `additionalTargetCondition: Condition.WithTag(Tag.Weapon)` 等，与 Haste/Slow 的 additionalTargetCondition 用法一致。

### 「触发条件」与「效果目标」勿混淆（AddAttribute/ReduceAttribute）

- **语义区分**：**Condition** = 何时触发（引起触发的物品 Item 需满足）；**TargetCondition** = 效果施加给谁（候选目标 Item 需满足）。
- **易错**：文案为「某条件下**此物品**获得 X」时（如「相邻物品触发减速时，此物品获得剧毒」），**Condition** 应描述「谁触发了该能力」（如被减速的物品与能力持有者相邻 → `condition: Condition.AdjacentToSource`），**TargetCondition** 应为 **SameAsSource**（只有能力持有者自己享受加属性），不能把「相邻」写在 `additionalTargetCondition` 上，否则会变成「己方且与 Source 相邻的物品」获得加成（即相邻物品而非本物品）。
- **正确写法**：`Ability.AddAttribute(..., targetCondition: Condition.SameAsSource, condition: Condition.AdjacentToSource, trigger: Trigger.Slow)`。反例（已修正）：失落神祇曾误用 `additionalTargetCondition: Condition.AdjacentToSource`，导致剧毒加给了相邻物品。

---

## 光环（Aura）与属性读取

### 使用时机与集成方式

- **仅战斗内**：光环在「读取属性」时生效；局外/UI（ItemDescHelper、MainWindow）直接调用 `ItemTemplate.GetInt(key, tier)`，不传上下文，不参与光环。
- **集成到 GetInt**：`ItemTemplate.GetInt(key, tier, defaultValue, IAuraContext? context)`：当 `context != null` 时，先取基础值再按公式叠加光环：`最终值 = (基础 + Σ 固定) × (1 + Σ 百分比/100)`，多光环的固定与百分比均为加算；被摧毁的物品不提供光环。
- **IAuraContext**：在 Core 中定义，仅提供 `GetAuraModifiers(attributeName, out fixedSum, out percentSum)`。BattleSimulator 实现为 `BattleAuraContext(side, targetItem, opp?)`（targetItem 为 `BattleItemState`），在 `GetAuraModifiers` 内遍历己方未摧毁物品的 `Template.Auras`，按条件谓词与属性名累加。

### 光环数据与条件

- **AuraDefinition**（Core）：`AttributeName`、`Condition`（`AuraConditionKind`）、`FixedValueKey`、`PercentValueKey`。条件首期支持 `AdjacentToSource`（`|sourceIndex - targetItemIndex| == 1`）；BuildSide 与 ItemDatabase 克隆模板时对 Auras 做深拷贝（逐个 `new AuraDefinition`）。
- **战斗内读属性**：需要光环时传入 context，例如暴击率：`item.Template.GetInt("CritRatePercent", item.Tier, 0, auraContext)`；模拟器在能力队列处理处按当前 `entry.Owner`（BattleItemState）创建 `BattleAuraContext(side, entry.Owner, opp)` 并传入。
- **效果数值必须带光环上下文**：`IEffectApplyContext.GetResolvedValue` 用于效果委托内取 Damage、Shield 等。若实现内只调 `Item.Template.GetInt(key, tier, default)` 而不传 `IAuraContext`，则「基础 0 + 光环加成」类物品（如废品场长枪）施放时伤害仍为 0。**正确做法**：`EffectApplyContextImpl.GetResolvedValue` 内创建 `new BattleAuraContext(Side, Item)` 并调用 `GetInt(key, tier, default, auraContext)`（Item 即 CasterItem），使施放时读取的数值已含光环。

### 光环公式（Formula + AuraFormulaEvaluator）

- **避免魔法字符串**：`AuraDefinition.FixedValueFormula` 使用 `Core/Formula.cs` 中的常量，如 `Formula.SmallCountStash`，不在物品或 BattleAuraContext 内手写 `"SmallCountStash"`。
- **公式逻辑不堆在 BattleAuraContext**：固定加成公式的实现放在 `BattleSimulator/AuraFormulaEvaluator.cs`。`BattleAuraContext.GetAuraModifiers` 当 `FixedValueFormula` 非空时调用 `AuraFormulaEvaluator.Evaluate(formulaName, source, side)`，不再在 BattleAuraContext 内写长 if-else。
- **新增公式**：在 `Formula` 中加常量；在 `AuraFormulaEvaluator.Evaluate` 的 switch 中加 case，并实现对应私有方法（如 `EvaluateSmallCountStash`）。克隆 AuraDefinition 时需复制 `FixedValueFormula`（ItemDatabase、BattleSimulator 的 BuildSide 均已处理）。

### 描述占位符：前缀/后缀

- **语法**：大括号内可为 `{前缀Key后缀}`，前缀/后缀为紧挨 key 两侧的「非字母、非数字、非下划线」字符，key 为字母/数字/下划线（如 `{+Custom_0%}` → key=Custom_0，前缀=+，后缀=%）。
- **替换与样式**：单 tier 替换为 `前缀+数值+后缀`；全 tier 为 `前缀+值1+后缀 » 前缀+值2+后缀 » …`。前缀与后缀随数值一起纳入 valueRanges，加粗与 tier 颜色作用于整段（如 "+3%" 整体高亮）。
- **实现**：`ItemDescHelper` 用正则解析占位符，不再维护固定 Placeholders 列表；`{Cooldown}` 仍映射为 key `CooldownMs` 并按秒格式化。

### 暴击与日志、关键词样式

- **暴击日志**：`IBattleLogSink.OnEffect(..., bool isCrit = false)`；当 `isCrit` 为 true 时，Console/TextBox/File 等 sink 在效果行末尾追加「 （暴击）」。
- **关键词着色**：`EffectKeywordFormatting` 中「伤害」「灼烧」「暴击」「暴击率」「暴击伤害」等仅修改颜色，**不加粗**；加粗仅用于物品名称和嵌入的数值区段（由 ItemDescHelper 的 valueRanges 控制）。「飞行」与护盾同色（rgb(244,207,32)），Tooltip 与战斗日志一致。

---

## 飞行机制、造成暴击时与战斗内属性统一带光环

### 飞行（In Flight）

- **运行时状态**：`BattleItemState.InFlight`，战斗开始为 false；由「开始飞行」「结束飞行」类效果修改。
- **效果**：`Effect.StartFlying` 调用 `ctx.SetCasterInFlight(true)` 并 `ctx.LogEffect("开始飞行", 0, showCrit: false)`。若 `ctx.IsCasterInFlight` 已为 true 则**不**设置、不记日志（幂等）。
- **光环条件**：`Condition.InFlight` 表示被评估对象（Item）在飞行。光环「提供者在飞行」用 **AuraDefinition.SourceCondition = Condition.InFlight**，评估时 Item=Source=提供者；如「此物品飞行时 +1 多重释放」为 Condition=SameAsSource、SourceCondition=InFlight。
- **日志与 UI**：`EffectLogFormat.FormatEffectValue("开始飞行", value)` 返回空串，避免显示 0；`EffectKeywordFormatting` 中「飞行」与护盾同色。

### 造成暴击时（Trigger.Crit）

- **语义**：与 Freeze/Slow 统一——**任意物品造成暴击时触发**；默认 `Condition.SameSide` 表现为己方暴击时触发，可重写 Condition（如 `DifferentSide`）实现对方暴击时触发。
- **触发时机**：`ExecuteOneEffect` 内所有效果执行完毕后，若 `isCrit == true` 则调用 `InvokeTrigger(Trigger.Crit, item, null, ...)`（item 即施放者 BattleItemState）；条件评估与其余触发器一致（Source=能力持有者，Item=暴击施放者）。
- **条件**：`EnsureTriggerCondition(Trigger.Crit)` 默认 `Condition.SameSide`。

### 战斗内属性统一带光环（BattleSide.GetItemInt）

- **原则**：游戏运行时读取任意物品字段都应包含光环上下文，避免「依赖变量的光环」漏算（如 Burn += Damage 时读 Damage 也需光环）。
- **统一入口**：`BattleSide.GetItemInt(itemIndex, key, defaultValue)` 内部用 `new BattleAuraContext(this, Items[itemIndex])` 调用 `Items[itemIndex].Template.GetInt(key, tier, default, context)`。
- **调用点**：BattleSimulator 步骤 7（AmmoCap、Multicast）、ProcessCooldown（CooldownMs、AmmoCap）；EffectApplyContextImpl 内 ChargeCasterItem、GetTargetIndices、ChargeItemAt、HasLifeSteal 等，凡有 (side, item) 的读属性均用 `side.GetItemInt(item.ItemIndex, ...)` 或等效。
- **光环内部**：`BattleAuraContext.GetAuraModifiers` 中 FixedValueKey/PercentValueKey 的读取带 `new BattleAuraContext(side, source)`；`AuraFormulaEvaluator.Evaluate(formulaName, source, side, opp)` 依赖来源属性时（如 `Formula.SourceDamage`）用 `BattleAuraContext(side, source)` 读值。

### Formula.SourceDamage 与依赖变量的光环

- **用途**：如「Burn = 0 + 自身 Damage（含光环）」：Aura 的 `AttributeName = Burn`、`FixedValueFormula = Formula.SourceDamage`，SameAsSource。
- **实现**：`AuraFormulaEvaluator.Evaluate(formulaName, source, side, opp)`；case `Formula.SourceDamage` 返回 `source.Template.GetInt("Damage", source.Tier, 0, new BattleAuraContext(side, source))`，保证读 Damage 时带光环且不形成 Burn↔Burn 循环。

---

## 避免魔法字符串（Tag / Trigger / nameof）

### 设计选择

- **标签**：`Core/Tag.cs` 提供 `Tag.Weapon`、`Tag.Tool`、`Tag.Apparel`、`Tag.Friend`、`Tag.Food`、`Tag.Tech` 等常量，对应「武器」「工具」「服饰」「伙伴」「食物」「科技」。物品的 `Tags = [Tag.Weapon]`、`Tags = [Tag.Friend, Tag.Tech]` 等，条件与效果处用 `Tags.Contains(Tag.Weapon)`，不再手写字符串。
- **触发器**：`Core/Trigger.cs` 提供 `Trigger.UseItem`、`Trigger.BattleStart`。能力定义用 `TriggerName = Trigger.UseItem`；模拟器判断用 `ab.TriggerName == Trigger.BattleStart`、`ab.TriggerName != Trigger.UseItem`。
- **属性名与 key**：AuraDefinition 的 `AttributeName`、`FixedValueKey`、`PercentValueKey` 以及 Effect 的 `ValueKey` 使用 `nameof(ItemTemplate.xxx)`，如 `nameof(ItemTemplate.CritRatePercent)`、`nameof(ItemTemplate.Custom_0)`。BattleSimulator 中 `GetInt(nameof(ItemTemplate.CritDamagePercent), ...)` 等同理，重命名属性时编译期可发现漏改。

### 小结

新增物品或效果时优先用 `Tag.*`、`Trigger.*`、`nameof(ItemTemplate.属性)`，避免散落中英文魔法字符串；仅 ItemTemplate 尚未暴露的属性（如 Shield、Regen）在 EffectKindKeys 等处保留字面量。

---

## 物品定义简化：DefaultSize/DefaultMinTier、Ability 工厂与 RegisterAll

本节记录物品定义与注册方式的简化经验，便于新增物品时统一风格。

### 尺寸与最低档位由 RegisterAll 按批次设置

- **不在每个物品中写 MinTier/Size**：物品工厂方法（如 `Fang()`）只设置 Name、Desc、Tags、数值、Abilities 等，**不**设置 `MinTier`、`Size`。
- **ItemDatabase**：提供 **`DefaultSize`**、**`DefaultMinTier`**；**`Register(ItemTemplate template)`** 在存入前执行 `template.Size = DefaultSize`、`template.MinTier = DefaultMinTier`。
- **RegisterAll 写法**：先设 `db.DefaultSize = ItemSize.Small`（或 Medium/Large），再按档位设 `db.DefaultMinTier` 并连续 `db.Register(...)`。例如 CommonSmall 先 `DefaultMinTier = Bronze` 注册所有铜物品，再 `DefaultMinTier = Silver` 注册所有银物品；CommonMedium/CommonLarge 仅铜则设一次 Bronze 后注册全部。

### 能力优先级默认 Medium

- **AbilityDefinition.Priority** 默认值为 **`AbilityPriority.Medium`**。仅当能力优先级非 Medium 时在定义中显式写 `Priority = AbilityPriority.High` 等。

### Ability 工厂方法（Core/Ability.cs）

- **单效果 UseItem**：优先用 **`Ability.Damage()`**、**`Shield()`**、**`Heal()`**、**`Burn()`**、**`Poison()`**（默认 trigger=UseItem，可选 priority、condition、additionalCondition、invokeTargetCondition）。
- **加速/减速/冻结**：**`Ability.Haste()`**、**`Slow()`**、**`Freeze()`**（目标默认 SameSide/DifferentSide）；可选 **targetCondition** 完全代替默认、**additionalTargetCondition** 在默认上 And 追加。
- **加属性/减属性**：**`Ability.AddAttribute()`**、**`Ability.ReduceAttribute()`**（目标默认己方 SameSide / 敌方 DifferentSide）；**additionalTargetCondition** 在默认上追加，**targetCondition** 完全代替默认。
- **多效果或特殊**：拆成多条能力，或 `new AbilityDefinition { ..., Apply = Effect.XXXApply, ... }`。

### 时间属性：秒转毫秒统一为 ToMilliseconds

- **SecondsOrByTier** 仅保留 **`ToMilliseconds()`**（原 `ToFreezeMs`、`ToSlowMs`、`ToHasteMs` 已合并为同一语义）。FreezeSeconds、SlowSeconds、HasteSeconds 的 setter 均调用 `value.ToMilliseconds()` 写入模板内部毫秒列表。

### 小结

新增公共物品时：在对应 Common* 的工厂方法中不写 MinTier/Size；在 RegisterAll 中按尺寸与档位设 DefaultSize/DefaultMinTier 后注册。能力优先用 `Ability.*OnUseItem`，仅非默认优先级或非默认目标条件时传参；时间在定义中用秒，内部通过 ToMilliseconds 转毫秒。详见 **.cursor/rules/item-design.mdc**。

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
- **SecondsOrByTier**：`Core/ItemTemplate.cs` 中结构体，支持从 `double` / `double[]` 隐式转换；秒转毫秒统一用 **`ToMilliseconds()`**（原 ToFreezeMs/ToSlowMs/ToHasteMs 已合并），`FromFirstTierMs(ms)` 用于 getter。定义时写 `FreezeSeconds = new[] { 3.0, 4.0, 5.0, 6.0 }`，符合「物品定义中时间一律用秒」的约定（见 **.cursor/rules/project-conventions.mdc**）。
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
- **实现**：`EffectApplyContextImpl.GetTargetIndices(fromSide, targetCount, condition)` 构建候选池并 Fisher-Yates 不放回选取；`ApplyFreeze`/`ApplySlow`/`ApplyCharge`/`ApplyHaste` 均调用该逻辑。`IEffectApplyContext.TargetCondition` 由模拟器从当前能力的 `TargetCondition` 注入；`Effect.Haste` 读 `HasteTargetCount`（默认 1）并传 `ctx.TargetCondition` 给 `ApplyHaste`。

### 小结

新增「持续时间 + 多目标」类效果时：时间在模板中用秒、内部毫秒；多目标可复用 extraSuffix 与 FormatEffectValue；若有「剩余时间」且影响冷却，须保证**先处理冷却、再减少剩余时间**。目标选择统一为「有冷却 + Condition + 不放回」。触发器与条件见下节「Condition 与 Effect 委托化重构」。

---

## 摧毁（Destroy）与「施加摧毁时」触发器

- **Trigger.Destroy**：`Core/Trigger.cs` 中 `Destroy = "摧毁物品时"`。语义：**任意物品施加摧毁时触发**，实现同 Slow：Condition 判定施加摧毁的物品，InvokeTargetCondition 判定被摧毁的物品；`EnsureTriggerCondition` 默认 `Condition.SameSide`，能力上常用 `SameAsSource` 表示「仅施加摧毁的物品自身」触发。
- **执行顺序**：「施加摧毁」必须在**将目标标记为 Destroyed 之前**调用 `InvokeTrigger`，以便被毁物品自身能力仍可触发。实现：`Effect.DestroyNextItemToRightOfCaster` 找到右侧下一件未摧毁物品 `target` 后，调用 `OnDestroyApplied(i)`；回调内先 `InvokeTrigger(Trigger.Destroy, item, new TriggerInvokeContext { InvokeTargetItem = target })`，再 `target.Destroyed = true`（item 为施放者 ctx.CasterItem）。
- **ConditionContext**：无需扩展；与 Slow 相同，评估时恒有 Source=能力持有者；Condition 时 Item=施加摧毁者，InvokeTargetCondition 时 Item=被摧毁物品。被摧毁目标为大型或飞行时用 **`InvokeTargetCondition = Condition.WithTag(Tag.Large) | Condition.InFlight`**（尺寸 Tag 由注册时按 Size 自动添加；InFlight 表示被评估物品在飞行）。
- **Effect.DestroyNextItemToRightOfCaster**：从 `Item.ItemIndex + 1` 起向右扫描，取第一个 `!Side.Items[i].Destroyed` 的 i；若无则 return。找到后记日志「摧毁」+ extraSuffix（→[物品名]），再调用 `OnDestroyApplied(i)`（或未注入时直接设 `target.Destroyed = true`）。牵引光束：能力 1 UseItem High → 该效果；能力 2/3 Destroy Medium，SameAsSource 造成伤害，能力 3 额外 `InvokeTargetCondition = Condition.WithTag(Tag.Large) | Condition.InFlight` 再造成伤害。
- **日志与颜色**：`EffectLogFormat.FormatEffectValue("摧毁"|"修复", value)` 返回空串，日志只显示效果名与 extraSuffix；`EffectKeywordFormatting` 中「摧毁」rgb(255,50,120)，「修复」rgb(143,252,188)。见 **.cursor/rules/data-and-logging.mdc**。

---

## 修复（Repair）机制

- **语义**：修复将**已摧毁**的己方物品恢复为未摧毁，并重置其 `CooldownElapsedMs = 0`，使该物品重新进入冷却循环。无时长参数。
- **目标选择**：与充能/加速不同，目标池为**已摧毁**（`it.Destroyed == true`）且满足能力 `TargetCondition` 的物品，**不**要求有冷却时间；默认 `TargetCondition` 为 SameSide。不放回随机选取至多 `RepairTargetCount` 个。
- **实现**：`EffectApplyContextImpl` 内单独实现 `GetRepairTargetIndices(fromSide, targetCount, condition)`，池子过滤条件为 `Destroyed`，不检查 `GetCooldownMs()`。`ApplyRepair` 对选中目标执行 `Destroyed = false`、`CooldownElapsedMs = 0`，并记日志「修复」+ 实际目标数 + `→[物品名]`。
- **模板与效果**：`ItemTemplate.RepairTargetCount`（可单值或按等级，默认 1）；`Effect.Repair` 读该值并调用 `ctx.ApplyRepair(count, ctx.TargetCondition)`。占位符 `{RepairTargetCount}` 由 ItemDescHelper 解析。
- **标签**：新增 `Tag.Tech = "科技"`（`Core/Tag.cs`），用于如废品场维修机器人等科技类物品。
- **日志着色**：`EffectKeywordFormatting` 中「修复」使用 rgb(143,252,188)。

---

## ConditionContext 重构与触发器统一

本节记录 ConditionContext 收敛为四字段、触发器语义与命名统一的重构经验。

### 设计原则与四字段

- **语义**：Condition 表示「战斗时**一个物品**需满足的条件」，计算时可能涉及其他物品；上下文只提供己方/敌方状态与被评估（及参考）物品，**不**为具体场景（如 OnDestroy、UsedTemplate）增加字段。
- **ConditionContext 仅四字段**：`MySide`、`EnemySide`、`Item?`（被评估对象：Condition 时=引起触发的物品，InvokeTargetCondition 时=触发器指向的物品，TargetCondition 时=候选目标；可为 null）、`Source`（能力所属物品，恒非空）。同一方/相邻/右侧等由 Item 与 Source 的 SideIndex/ItemIndex 推导。
- **索引**：`BattleSide.SideIndex`、`BattleItemState.SideIndex`/`ItemIndex` 在 `BattleSimulator.Run` 中、`BuildSide` 之后统一写入，供 Condition 与调用方使用。
- **扩展性**：新需求通过「调用方传入不同的 Item/Source」或组合 Condition（如 `WithTag(Tag.Large) | InFlight`）满足，避免在 Context 上堆场景专用字段。

### 触发器统一语义与命名

- **统一语义**：Freeze、Slow、Crit、Destroy 均为「**任意物品**施加/造成 xx 时触发」；默认 `Condition.SameSide` 表现为己方，重写 Condition（如 `DifferentSide`）可实现对方触发。与 UseItem（仅己方施放）、BattleStart 一致由 `InvokeTrigger` 统一处理，Condition 评估时 Source=能力持有者、Item=引起触发的物品。
- **命名统一**：触发器常量与 UseItem/Freeze/Slow 保持一致风格：`Trigger.Crit`、`Trigger.Destroy`（不再使用 OnCrit、OnDestroy），便于后续扩展新触发器时命名一致。
- **Destroy 与 Slow 同构**：施加摧毁时用 Condition 判定施加者、InvokeTargetCondition 判定被摧毁物品，context 传 `InvokeTargetItem`（BattleItemState 引用），无需 DestroyedItemTemplate 等专用字段；被毁目标为大型或飞行用 `InvokeTargetCondition = Condition.WithTag(Tag.Large) | Condition.InFlight`。

---

## Condition 与 Effect 委托化重构

本节记录 Condition / Effect 脱离枚举与 switch、改为委托驱动的重构经验，便于后续扩展触发器条件与效果时对照。

### Condition：委托 + ConditionContext，支持 And 组合

- **移除**：`ConditionKind` 枚举与 `Tag` 字段；原基于 switch 的 `TriggerConditionEvaluator` / `AuraConditionEvaluator`。
- **Core/Condition.cs**：`Condition` 类仅持有一个 `Func<ConditionContext, bool>?` 委托；`ConditionContext` 为只读结构体，仅含 `MySide`、`EnemySide`、`Item?`（被评估对象，可为 null）、`Source`（能力所属物品，非空）；同一方/相邻等由 Item 与 Source 的 SideIndex/ItemIndex 推导。评估时调用 `condition.Evaluate(ctx)`。
- **静态工厂**：`SameAsSource`、`DifferentFromSource`、`SameSide`、`AdjacentToSource`、`RightOfSource`、`LeftOfSource`、`WithTag(tag)`（被评估对象带 tag）、`InFlight`（被评估对象在飞行）；可用 `&` / `|` 组合。能力持有者需满足某条件时在 **AbilityDefinition.SourceCondition** 中填写，评估时 Item=Source=能力持有者，故用 `WithTag(tag)`、`InFlight` 即可表达「本物品带 tag/在飞行」。尺寸 Tag（`Tag.Small`/`Medium`/`Large`）由注册时按 `template.Size` 自动添加。
- **Condition 收敛与 SourceCondition**：Condition 只描述「被评估对象（Item）是否满足」，不区分场景；`WithTag(tag)`、`InFlight` 均仅看 Item，避免 ItemWithTag/SourceWithTag、SourceInFlight 等重复定义。「能力持有者/光环提供者需满足」统一用 **SourceCondition**：**AbilityDefinition.SourceCondition** 与 **AuraDefinition.SourceCondition** 在评估时构造上下文 Item=Source=能力持有者/提供者，复用同一套 Condition（如 `WithTag(Tag.Weapon)`、`InFlight`）。克隆模板时须同时克隆 Condition 与 SourceCondition。
- **克隆**：`Condition.Clone(c)` 在 Core 中提供，复制委托引用，供 BuildSide / ItemDatabase 克隆能力时使用。

### UseOtherItem 默认与显式 Condition：始终叠加己方

- **问题**：若 UseOtherItem 仅在有显式 Condition（如姜饼人 `WithTag(Tag.Tool)`）时直接返回该 Condition，则不会限制「己方」，对方使用工具也会触发己方能力。
- **正确做法**：UseOtherItem **始终**先叠加「己方其他物品」基础条件 `baseSameSideOther = DifferentFromSource & SameSide`。若原 Condition 为 null，则 `return baseSameSideOther`；若非 null，则 `return baseSameSideOther & condition`。这样「使用工具时充能」等显式条件与「仅己方」同时满足。详见 **.cursor/rules/battle-simulator-ability-queue.mdc**。

### Effect：脱离 EffectKind，委托驱动 + IEffectApplyContext（后已合并入能力）

- **移除**：`EffectKind`、`GetEffectApplier`、`CustomEffectHandlers`；**EffectDefinition 类与 AbilityDefinition.Effects 列表**（已合并进 AbilityDefinition，见上文「能力与 Effect 合并重构」）。
- **Core**：`IEffectApplyContext` 提供 Value、GetResolvedValue、TargetCondition、各类 Apply/Log；**AbilityDefinition** 直接持有一条效果的 `Value`、`ValueKey`、`ApplyCritMultiplier`、`Apply`。预定义在 **Core/Effect.cs** 内为 *Apply 委托，只读效果用 `ctx.GetResolvedValue(...)` 取值。
- **暴击**：由物品六字段与 **ability.ApplyCritMultiplier** 决定；模拟器用 `ability.Apply != null && ability.ApplyCritMultiplier` 判断是否掷暴击。
- **BattleSimulator**：`ExecuteOneEffect` 对**单个能力**若 `ability.Apply != null` 则解析 value、构建 ctx、调用 `ability.Apply(ctx)`；仅当 `ability.ValueKey != null` 时解析并填入 `ctx.Value`。

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
- 同优先级时按 (Owner.SideIndex, Owner.ItemIndex, AbilityIndex) 作为次键，保证顺序稳定、可复现（AbilityQueueEntry.Owner 为 BattleItemState）。

这样同一帧内触发的多条能力会严格按「高优先级先于低优先级」执行，与设计意图一致。详见 **.cursor/rules/battle-simulator-ability-queue.mdc**。

---

## 物品类型 Tag（护盾/伤害/灼烧等）

- **Tag 常量**：`Core/Tag.cs` 提供 `Tag.Shield`、`Tag.Damage`、`Tag.Burn`、`Tag.Poison`、`Tag.Heal`、`Tag.Regen`，用于判断物品是否为护盾/伤害/灼烧等类型及是否可暴击。
- **注册时自动补充**：`ItemDatabase.Register` 在写入模板前调用 `EnsureTypeTags`：① 若模板任一档位下某属性（Damage、Burn、Poison、Heal、Shield、Regen）> 0，则向模板的 `Tags` 加入对应类型 Tag；② 若模板有光环且条件为 **SameAsSource**（作用目标为自身），则按光环的 **AttributeName** 若为上述六类属性之一也补充对应 Tag（如 Damage 为 0 但由光环提供伤害的废品场长枪仍会得到 Tag.Damage）。无需在物品定义里手写。
- **判断时使用 Tag**：模拟器判断「是否可暴击」用 `ItemHasAnyCrittableField(item)`，内部看 `item.Template.Tags` 是否含上述六类 Tag 之一；裂盾等效果用 `Condition.WithTag(Tag.Shield)`，内部看 `ctx.Item.Template.Tags.Contains(Tag.Shield)`。类型由 Tag 决定，不受战斗内数值修改（如裂盾减 Shield）影响。

### 经验总结

- **为何用 Tag 不用快照**：战斗内会修改模板数值（如裂盾减 Shield），若用当前数值判断「是否护盾/可暴击」会误判；用注册时写入的 Tag 则类型稳定。
- **为何要按光环补 Tag**：部分物品模板上某属性为 0，实际数值完全由光环提供（如废品场长枪 Damage=0、光环 SameAsSource + AttributeName=Damage）；仅看属性会漏打 Tag，导致可暴击等逻辑错误。只对 **Condition == SameAsSource** 的光环按 **AttributeName** 补 Tag，避免把「给相邻武器加伤」等光环误当作自身类型。

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
| **TriggerInvokeContext.cs** | 触发器调用上下文（Multicast、UsedTemplate、InvokeTargetItem）。 |

以上类型均为 `internal`，仅本程序集使用。主逻辑与帧循环保留在 **BattleSimulator.cs**，便于阅读与遵守 **.cursor/rules/battle-simulator-ability-queue.mdc**。

---

## 伤害报表与效果类型（吸血、护盾降低、伤害提高）

### 问题：吸血伤害未统计

- **现象**：毒刺等带 `LifeSteal` 的物品造成伤害时，`Effect.Damage` 为区分展示会以 **「吸血」** 调用 `LogEffect`（`ctx.LogEffect(ctx.HasLifeSteal ? "吸血" : "伤害", value, ...)`），而 **StatsCollectingSink** 的 `OnEffect` 仅对 `effectKind == "伤害"` 累加 `a.Damage` 与 `AddSide(damage: value)`，导致吸血那一下的数值未进入伤害报表。
- **修复**：在 **StatsCollectingSink** 的 switch 中，将 **「吸血」** 与 **「伤害」** 同等处理：`case "伤害": case "吸血":` 均执行 `a.Damage += value` 与 `AddSide(caster.SideIndex, damage: value)`。吸血与伤害为同一数值，仅展示不同，统计时均计入伤害。OnEffect 签名为 `(BattleItemState caster, ...)`。

### 定向效果日志格式统一

- **约定**：定向/多目标效果统一为「[物品名] 效果名 数值 →[目标]」形式，便于报表与日志解析一致。
- **护盾降低**：裂盾刀等减少对方护盾物品 Shield 时，日志用 **「护盾降低」**（不再用「裂盾」），`extraSuffix = " →[" + string.Join("、", targetNames) + "]"`。
- **伤害提高**：举重手套、暗影斗篷等增加武器伤害时，日志用 **「伤害提高」**（不再用「武器伤害提升」），由 `AddWeaponDamageBonusToCasterSide` / `AddWeaponDamageBonusToCasterSideItem` 内建 targetNames 并调用 `LogEffect("伤害提高", value, extraSuffix)`。报表统计不依赖 effectKind 为「伤害提高」（该效果不进入 Damage 累加），仅伤害类效果（伤害/吸血）计入 Damage。

### 小结

新增或修改 `effectKind` 时，若该效果**实质为造成伤害**（如吸血），需在 **StatsCollectingSink.OnEffect** 中与「伤害」一并计入 `a.Damage` 与 `AddSide(damage)`；定向效果命名与格式见 **data-and-logging.mdc**。

---

## UseOtherItem 右侧条件与加速效果

本节记录「暗影斗篷」等「使用此物品右侧物品时」触发、以及加速（Haste）效果的实现经验。

### Condition.LeftOfSource 与 RightOfSource

- **RightOfSource**（Item 在 Source 右侧，即 `Item.ItemIndex == Source.ItemIndex + 1`）。UseItem 触发时 Source=能力持有者、Item=被使用物品，故「被使用物品在能力持有者右侧」= SameSide & RightOfSource；目标选择时「施放者右侧物品」亦用 RightOfSource（Source=施放者、Item=候选）。
- **LeftOfSource**（Item 在 Source 左侧），用于目标选择等。
- **用途**：暗影斗篷等 `TriggerName = Trigger.UseItem`、`Condition = DifferentFromSource & SameSide & RightOfSource`，能力内用 `Effect.Haste` 并设 `TargetCondition = Condition.RightOfSource`、`HasteTargetCount = 1`，对右侧有冷却物品施加加速。

### 加速（Haste）与 Effect.Haste

- **与减速对称**：`BattleItemState` 已有 `HasteRemainingMs`；每帧先处理冷却（加速时 `advanceMs *= 2`），再在步骤 3 中 `HasteRemainingMs = Math.Max(0, HasteRemainingMs - FrameMs)`。
- **模板**：`ItemTemplate` 提供 `Haste`（毫秒）、`HasteSeconds`（秒，可单值或按等级）、`HasteTargetCount`（目标数，默认 1）；物品定义用 `HasteSeconds = new[] { 1.0, 2.0, 3.0, 4.0 }`。
- **效果**：**Effect.Haste**（已移除 Effect.Accelerate）从模板读 `Haste`、`HasteTargetCount`（默认 1），调用 `ctx.ApplyHaste(hasteMs, count, ctx.TargetCondition)`；目标由统一逻辑选取（己方有冷却 + 满足能力 `TargetCondition`，默认 SameSide）。暗影斗篷在能力上设 `TargetCondition = Condition.RightOfSource`、`HasteTargetCount = 1`，不在 Effect 内写死。
- **日志与 UI**：`EffectLogFormat` 对「加速」将毫秒格式化为「N 秒」；`EffectKeywordFormatting` 中「加速」颜色 `rgb(0,236,195)`；`ItemDescHelper` 支持 `{HasteSeconds}` → `Haste`。

### 单目标武器伤害提高

- **AddWeaponDamageBonusToCasterSideItem(value, targetItemIndexOnCasterSide)**：仅对己方指定下标物品生效；若该物品带 `Tag.Weapon` 则 `Damage.Add(value)` 并 `LogEffect("伤害提高", value, " →[目标名]")`，非武器则不操作、不记日志。
- **Effect.WeaponDamageBonusToRightItem(ValueKey)**：从 ValueKey 取值，对 `ctx.CasterItem.ItemIndex + 1` 调用上述方法，用于暗影斗篷「若右侧为武器则伤害提高」。

---

## 开发与提交

- **Git 提交信息**：须符合 **.cursor/rules/git-commit-format.mdc**。格式为 `<type>(<scope>): <subject>`，主题用中文、句末无句号；type 取 `feat` / `fix` / `refactor` / `docs` / `chore`，scope 可选（如 `gui`、`sim`、`item-db`）。提交前可在 `docs/changelog.md` 中补充分条说明。
