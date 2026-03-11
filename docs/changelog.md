# 变更记录

## Condition 收敛与 SourceCondition 统一

- **ConditionContext 语义**：Source=能力持有者（恒非空），Item=被评估对象（Condition 时=引起触发的物品，InvokeTargetCondition 时=指向的物品，TargetCondition 时=候选目标；可为 null）。调用处构建上下文时统一按此赋值。
- **WithTag / InFlight 收敛**：仅保留「被评估对象（Item）」语义；移除 ItemWithTag、SourceWithTag、SourceInFlight。能力/光环「本物品带 tag/在飞行」用 **SourceCondition**，评估时 Item=Source。
- **SourceCondition**：**AbilityDefinition.SourceCondition** 与 **AuraDefinition.SourceCondition**；评估时 Item=Source=能力持有者/光环提供者，复用 WithTag(tag)、InFlight 等。宇宙护符「此物品飞行时 +1 多重释放」改为 Condition=SameAsSource、SourceCondition=InFlight。
- **尺寸 Tag**：Tag.Small/Medium/Large 由 Register 按 template.Size 自动添加；「大型或飞行」等用 `WithTag(Tag.Large) | InFlight`。
- **文档与规则**：implementation-notes 补充「Condition 收敛与 SourceCondition」；project-conventions、item-design、battle-simulator-ability-queue 同步条件语义与写法。

---

## ConditionContext 重构与触发器统一

- **ConditionContext**：收敛为四字段 `MySide`、`EnemySide`、`Item`、`Source?`；移除 CandidateSide/Item、SourceSide/Item、UsedTemplate、CandidateTemplate、SourceInFlight、DestroyedItem* 等场景字段。Condition 全部基于 Item/Source 的 SideIndex/ItemIndex/Template/InFlight 重写；新增 `Condition.LargeOrInFlight`，移除 `DestroyedTargetIsLargeOrInFlight`。
- **BattleSide / BattleItemState**：新增 `SideIndex`、`ItemIndex`（Run 初始化时写入），用于同侧/相邻等推导。
- **触发器语义统一**：Freeze/Slow/Crit/Destroy 均为「任意物品施加/造成 xx 时触发」，默认 Condition 为 SameSide；可重写实现对方触发。
- **触发器命名**：`OnDestroy` → `Trigger.Destroy`，`OnCrit` → `Trigger.Crit`，与 UseItem/Freeze/Slow 风格一致。
- **Destroy**：与 Slow 同构，Condition 判定施加者、InvokeTargetCondition 判定被摧毁物品；牵引光束能力 3 改为 `InvokeTargetCondition = Condition.LargeOrInFlight`。
- **TriggerInvokeContext**：移除 DestroyedItemTemplate、DestroyedItemInFlight。
- **文档与规则**：implementation-notes 新增「ConditionContext 重构与触发器统一」；battle-simulator-ability-queue、item-design、changelog 同步更新。

---

## AbilityDefinition 条件统一化与 UseOtherItem 移除

- **三种条件**：`Condition` = 引起触发的物品（source）需满足；`InvokeTargetCondition` = 触发器所指向物品需满足（如 Slow 时被减速物品，默认 null）；`TargetCondition` = 效果选目标时目标需满足。克隆与 `EnsureTriggerCondition` 仅做 condition ?? default（UseItem→SameAsSource，其他→SameSide）。
- **移除 Trigger.UseOtherItem**：步骤 7 仅调用一次 `InvokeTrigger(Trigger.UseItem, ...)`；「其他物品使用则触发」改为 `Trigger.UseItem` + `Condition = And(DifferentFromSource, SameSide)[ + 额外条件 ]`。神经毒素、断裂镣铐、姜饼人、暗影斗篷四处物品已迁移。
- **TriggerInvokeContext**：新增 `InvokeTargetSideIndex`、`InvokeTargetItemIndex`；Slow/Freeze 按每个目标调用一次 InvokeTrigger，支持 InvokeTargetCondition 筛选。
- **EffectApplyContextImpl**：`OnFreezeApplied`/`OnSlowApplied` 改为传递目标列表 `(sideIndex, itemIndex)[]`。
- **Ability 工厂**：Damage、Shield、Heal、Burn、Poison、Haste、Slow、Freeze 支持可选 **condition**、**additionalCondition**（仅作参数，工厂内与默认合并后写入 Condition）、**invokeTargetCondition**；默认 trigger=UseItem。
- **文档与规则**：implementation-notes 新增「AbilityDefinition 条件统一化与 UseOtherItem 移除」；project-conventions、item-design、battle-simulator-ability-queue 更新 Ability 工厂名与条件语义。

---

## 护盾/伤害等用 Tag 判断、移除 ItemTypeSnapshot

- **类型 Tag**：`Core/Tag.cs` 新增 `Tag.Shield`、`Tag.Damage`、`Tag.Burn`、`Tag.Poison`、`Tag.Heal`、`Tag.Regen`，用于判断护盾/伤害/灼烧等物品及是否可暴击。
- **注册时自动补充**：`ItemDatabase.Register` 内根据模板属性（任一档位 > 0）自动向 `Tags` 加入对应类型 Tag；若属性为 0 但存在 **Condition 为 SameAsSource** 的光环且 **AttributeName** 为六类之一，也补充对应 Tag（如废品场长枪 Damage=0 由光环提供仍得 Tag.Damage）。无需在物品定义中手写。
- **Condition 与可暴击**：`Condition.IsShieldItem` 改为依据 `CandidateTemplate.Tags.Contains(Tag.Shield)`；可暴击判定改为 `ItemHasAnyCrittableField` 检查模板是否含上述六类 Tag 之一；移除 `ConditionContext.CandidateTypeSnapshot`。
- **删除**：移除 `Core/ItemTypeSnapshot.cs`、`BattleItemState.TypeSnapshot` 及 BuildSide 中 TypeSnapshot 赋值；`EffectApplyContextImpl.ReduceAttributeToOpponentSide` 不再设置 `CandidateTypeSnapshot`。
- **文档与规则**：implementation-notes「物品类型快照」改为「物品类型 Tag」并增加经验总结（为何用 Tag、为何按光环补 Tag）；item-design.mdc、project-conventions.mdc、changelog 相应更新。

---

## 物品定义简化：DefaultSize/DefaultMinTier、Ability 工厂、Priority 默认、ToMilliseconds

- **物品注册**：MinTier、Size 不再在每个物品工厂中定义；**ItemDatabase** 提供 **DefaultSize**、**DefaultMinTier**，**Register** 时写入模板。**RegisterAll** 中按批次设置（如 `db.DefaultSize = ItemSize.Small`，再 `db.DefaultMinTier = Bronze` 注册铜、再 Silver 注册银）；CommonSmall 注册顺序为先铜后银。
- **能力优先级**：**AbilityDefinition.Priority** 默认 **Medium**，仅非默认时在能力定义中显式写 Priority。
- **Ability 工厂（Core/Ability.cs）**：新增 **DamageOnUseItem**、**ShieldOnUseItem**、**HealOnUseItem**、**BurnOnUseItem**、**PoisonOnUseItem**（可选 priority）；**HasteOnUseItem**、**SlowOnUseItem**、**FreezeOnUseItem**（可选 priority、targetCondition 代替默认、additionalTargetCondition 在默认上追加）。物品定义中单效果 UseItem 改为使用上述工厂，多效果或特殊 Condition 仍用 `new AbilityDefinition { ... }`。
- **SecondsOrByTier**：**ToFreezeMs/ToSlowMs/ToHasteMs** 合并为 **ToMilliseconds()**，FreezeSeconds/SlowSeconds/HasteSeconds 的 setter 统一调用。
- **文档与规则**：implementation-notes 新增「物品定义简化：DefaultSize/DefaultMinTier、Ability 工厂与 RegisterAll」；project-conventions.mdc 补充物品注册与 Ability 工厂约定；item-design.mdc 更新 MinTier/Size、触发器与能力、新增物品流程及 ToMilliseconds；冰锥一节中 SecondsOrByTier 描述改为 ToMilliseconds。

---

## 牵引光束、摧毁触发器、摧毁/修复日志与规则文档

- **牵引光束（Tractor Beam）**：6s 小 银 武器；使用物品时摧毁己方右侧下一件未摧毁物品（`Effect.DestroyNextItemToRightOfCaster`），造成 150»300»600 伤害；若被毁物品为大型或飞行再造成等量伤害。三能力：UseItem High → 摧毁；OnDestroy Medium SameAsSource → 伤害；OnDestroy Medium SameAsSource + DestroyedTargetIsLargeOrInFlight → 再伤害。
- **摧毁物品时（Trigger.OnDestroy）**：新增触发器；须在标记 Destroyed 之前调用 InvokeTrigger。`ConditionContext`/`TriggerInvokeContext` 增加 `DestroyedItemTemplate`、`DestroyedItemInFlight`；`Condition.DestroyedTargetIsLargeOrInFlight` 供能力 3 判定。`IEffectApplyContext.DestroyNextItemToRightOfCaster`、`Effect.DestroyNextItemToRightOfCaster`；`EffectApplyContextImpl.OnDestroyApplied` 由模拟器注入，回调内先触发再标记。
- **摧毁/修复 日志**：`EffectLogFormat` 对「摧毁」「修复」返回空串，日志行只显示效果名与 extraSuffix（不显示「摧毁 0」「修复 1」）；各 log sink 仅当 valueStr 非空才拼接空格与数值。「摧毁」颜色 rgb(255,50,120)，`EffectKeywordFormatting` 与 data-and-logging.mdc 已补充。
- **文档与规则**：implementation-notes 新增「摧毁（Destroy）与『摧毁物品时』触发器」；item-design.mdc 补充 Tag.Drone/Toy、Trigger.OnDestroy、Effect.DestroyNextItemToRightOfCaster、摧毁/修复日志约定；data-and-logging.mdc 补充摧毁/修复显示与颜色；battle-simulator-ability-queue.mdc 补充 OnDestroy；TriggerInvokeContext 表更新。

---

## 飞行机制、Crit、统一光环读取与三件新物品

- **飞行（In Flight）**：`BattleItemState.InFlight` 运行时状态；`Effect.StartFlying` 设置并记日志「开始飞行」，若已在飞行则不重复结算（幂等）。光环「提供者在飞行」用 AuraDefinition.SourceCondition = Condition.InFlight。Tooltip/日志「飞行」与护盾同色。
- **造成暴击时**：`Trigger.Crit`；`ExecuteOneEffect` 结束后若 `isCrit` 则 `InvokeTrigger(Trigger.Crit, ...)`；`EnsureTriggerCondition` 默认 SameSide。
- **战斗内属性统一带光环**：`BattleSide.GetItemInt(itemIndex, key, default)` 统一入口；BattleSimulator、EffectApplyContextImpl、BattleAuraContext 中战斗内读属性改为通过 GetItemInt 或带 context 的 GetInt。光环内 FixedValueKey/PercentValueKey 与公式求值（含 `Formula.SourceDamage`）均带 `BattleAuraContext(side, sourceIndex)`。
- **Formula.SourceDamage**：光环固定加成 = 来源物品 Damage（含光环），用于如巨龙崽崽 Burn = 自身 Damage；`AuraFormulaEvaluator.Evaluate` 增加 `sourceIndex` 参数。
- **Tag.Dragon**、**按 tier 冷却**：物品可设 `CooldownMs = [9000, 8000, 7000]`；新增 `Tag.Dragon = "巨龙"`。
- **三件新物品**：断裂镣铐（8s 银 小，武器伤害+4/8/12、使用武器时充能 2s）、宇宙护符（5s 银 小 遗物，加速 1 件 1/2/3s、暴击时开始飞行、飞行时 +1 多重释放）、巨龙崽崽（9/8/7s 银 小 武器 伙伴 巨龙，5 伤害、灼烧等量伤害 Aura、开始飞行）。文档与规则：item-design.mdc、battle-simulator-ability-queue.mdc、implementation-notes.md 已更新。

---

## MinTier/IntOrByTier 约定、卡组 GUI tier、Git 提交格式

- **IntOrByTier 与 MinTier**：最小 tier 为银/金/钻的物品，其按等级列表仅存该 tier 起的数值（如最小银则 list 为 [银,金,钻] 三档）。**ItemTemplate.GetInt(key, tier)** 按 `listIndex = (int)tier - (int)MinTier` 映射列表下标；越界时用首/末档兜底。属性读取与战斗逻辑统一按此约定。
- **可加入物品区展示**：**ItemDescHelper.ReplacePlaceholdersAllTiers** 直接使用模板列表（不再 Skip(minTierIdx)），数值段着色用 `(ItemTier)(minTierIdx + i)`，最小银物品显示银、金、钻石三色。
- **卡组 GUI**：拖拽入卡组时使用 **template.MinTier** 作为初始 tier；若当前等级不允许该 tier（如 1–2 级拖最小银物品）则禁止放入（DragOver 设 None、Drop 不插入）。卡组内点击 tier 块切换时 **CycleToNextAllowedTier** 仅在各物 MinTier 及以上且等级允许的 tier 间循环。OverridableAttributes 取值用 `listIndex = (int)tier - (int)MinTier`。
- **Git 提交信息格式**：新增 **.cursor/rules/git-commit-format.mdc**，约定 `<type>(<scope>): <subject>`（中文主题，type: feat/fix/refactor/docs/chore）。**project-conventions.mdc** 与 **docs/implementation-notes.md** 增加对提交格式的引用与简要说明。

---

## AddAttribute/ReduceAttribute 统一、默认参数、文档与规则

- **Effect.AddAttribute / ReduceAttribute**：己方加属性、敌方减属性统一用 `Effect.AddAttribute(attributeName, amountKey?, targetCondition?)` 与 `Effect.ReduceAttribute(...)`；默认 `amountKey = Custom_0`、`targetCondition = SameAsSource`，简化「自身 + Custom_0」类效果（如失落神祇 `Effect.AddAttribute(nameof(ItemTemplate.Poison))`）。
- **过时 API 移除**：删除 `WeaponDamageBonus`、`WeaponDamageBonusToRightItem` 及 `AddWeaponDamageBonusToCasterSide`、`AddWeaponDamageBonusToCasterSideItem`、`AddPoisonToCasterSideItem`、`ReduceOpponentShieldItemsShield`；举重手套、暗影斗篷、冰冻钝器、失落神祇、裂盾刀均改用 AddAttribute/ReduceAttribute。
- **ReduceAttribute 与 Condition**：`ConditionContext.CandidateTypeSnapshot` 可选，在 ReduceAttribute 遍历敌方时填入；`Condition.IsShieldItem` 供裂盾刀等「敌方护盾物品」筛选使用。（后已改为用 Tag 判断，见下。）
- **文档与规则**：implementation-notes 新增「AddAttribute / ReduceAttribute 与统一属性增减」；item-design.mdc 更新效果约定（ValueKey、AddAttribute/ReduceAttribute 优先、Tag.Relic）、新增物品流程中效果列表。

---

## CommonLarge、弹药、光环公式、属性复写与效果数值带光环

- **CommonLarge**：新建 `ItemDatabase/CommonLarge.cs`，五大公共大型物品：临时避难所（7s 铜 大 地产，护盾 10»20»40»80）、哈库维发射器（3s 铜 大 武器，伤害 100»200»300»400，弹药 1）、观光缆车（5s 铜 大 载具，护盾 20»40»80»160）、温泉（6s 铜 大 地产，治疗 25»50»100»200）、废品场长枪（11s 铜 大 武器，Damage 基础 0，光环 SmallCountStash）。App/Program 中调用 `CommonLarge.RegisterAll`。
- **标签**：`Tag.Property = "地产"`、`Tag.Vehicle = "载具"`（`Core/Tag.cs`）。
- **弹药**：`AmmoCap`、`AmmoRemaining` 已有；`IBattleLogSink.OnCast` 增加可选参数 `int? ammoRemainingAfter`，使用后日志显示「剩余弹药 N」；`EffectKeywordFormatting` 增加「弹药」颜色 rgb(255,142,0)。
- **光环公式**：`AuraDefinition.FixedValueFormula` 可选；公式名用 `Formula.SmallCountStash` 等常量（`Core/Formula.cs`），避免魔法字符串。公式逻辑集中在 `BattleSimulator/AuraFormulaEvaluator.cs`，不在 `BattleAuraContext` 内堆 if-else；新增公式时在 Formula 加常量、在 AuraFormulaEvaluator 加 case 与私有方法。
- **废品场长枪**：`StashParameter`（IntOrByTier）、`OverridableAttributes`（`Dictionary<string, IntOrByTier>?`）；光环 `FixedValueFormula = Formula.SmallCountStash`，固定加成 = Custom_0 × (己方未摧毁小型物品数 + StashParameter)。克隆模板/光环时复制 `FixedValueFormula`、`OverridableAttributes`。
- **效果数值与光环**：施放时效果取值必须带光环上下文，否则「基础 0 + 光环加成」类物品（如废品场长枪）伤害仍为 0。`EffectApplyContextImpl.GetResolvedValue` 内创建 `BattleAuraContext(Side, ItemIndex)` 并调用 `Item.Template.GetInt(key, tier, default, auraContext)`。
- **卡组属性复写**：`DeckSlotEntry.Overrides` 已有；`SlotRowViewModel.Overrides`、`SetOverride`/`RemoveOverride`；加入卡组或改 Tier 时按模板 `OverridableAttributes` 初始化/更新 Overrides。BuildDeckFromEditor / LoadDeckIntoEditor 读写 Overrides。GUI：名称行下方增加「复写」按钮行，名称行显示物品名 + 每行一个复写（如 StashParameter: 2）；点击复写打开 `OverrideAttributeDialog`（下拉选属性、填数值）。
- **Tier 变更后刷新**：切换 Tier 后调用 `RebuildDeckNameRow()`，复写显示立即更新。
- **文档与规则**：implementation-notes 增加「光环公式（Formula + AuraFormulaEvaluator）」「效果数值必须带光环上下文」；item-design.mdc 补充 Tag.Property/Vehicle、AmmoCap、StashParameter、OverridableAttributes、FixedValueFormula/Formula、CommonLarge、复写流程；battle-simulator-ability-queue.mdc 补充 GetResolvedValue 与光环。

---

## 修复（Repair）机制、Tech 标签、废品场维修机器人

- **修复效果**：新增 `Effect.Repair`，仿照加速：从模板读 `RepairTargetCount`（默认 1），调用 `ApplyRepair(count, ctx.TargetCondition)`。目标池为**己方已摧毁**物品（不要求有冷却），不放回随机选取至多 N 个；修复后 `Destroyed = false`、`CooldownElapsedMs = 0`。默认 `TargetCondition` 为 SameSide。
- **ItemTemplate.RepairTargetCount**：新增，可单值或按等级；与 `TargetCondition` 配合。无时长参数。
- **IEffectApplyContext.ApplyRepair**：新增；实现内使用 `GetRepairTargetIndices`（池子为 `it.Destroyed == true` + condition），与充能/加速的 `GetTargetIndices`（池子为未摧毁且有冷却）区分。
- **标签 Tech**：`Core/Tag.cs` 新增 `Tag.Tech = "科技"`。
- **废品场维修机器人**（Junkyard Repairbot）：5s 中 铜 伙伴+科技；两条 UseItem 能力——修复 1 件（Priority Lowest）、治疗 30»60»120»240（Priority Medium）；Desc 占位符 `{RepairTargetCount}`、`{Heal}`。
- **日志着色**：`EffectKeywordFormatting` 增加「修复」rgb(143,252,188)。
- **文档与规则**：implementation-notes 增加「修复（Repair）机制」；item-design.mdc 补充 Tag.Tech、RepairTargetCount、Effect.Repair 与目标池约定。

---

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
