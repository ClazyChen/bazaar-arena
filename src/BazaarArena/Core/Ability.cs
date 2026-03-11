namespace BazaarArena.Core;

/// <summary>常用能力定义的工厂方法，用于简化物品定义。</summary>
public static class Ability
{
    private static Condition MergeCondition(string trigger, Condition? condition, Condition? additionalCondition)
    {
        var defaultCond = trigger == Trigger.UseItem ? Condition.SameAsSource : Condition.SameSide;
        var final = condition ?? defaultCond;
        return additionalCondition != null ? (final & additionalCondition) : final;
    }

    /// <summary>造成伤害（Effect.DamageApply）。默认触发器 UseItem；可选 trigger、priority、condition、additionalCondition、invokeTargetCondition。</summary>
    public static AbilityDefinition Damage(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        ValueKey = nameof(ItemTemplate.Damage),
        ApplyCritMultiplier = true,
        Apply = Effect.DamageApply,
        Priority = priority ?? AbilityPriority.Medium,
    };

    /// <summary>获得护盾（Effect.ShieldApply）。默认触发器 UseItem；可选 trigger、priority、condition、additionalCondition、invokeTargetCondition。</summary>
    public static AbilityDefinition Shield(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        ValueKey = nameof(ItemTemplate.Shield),
        ApplyCritMultiplier = true,
        Apply = Effect.ShieldApply,
        Priority = priority ?? AbilityPriority.Medium,
    };

    /// <summary>治疗（Effect.HealApply）。默认触发器 UseItem；可选 trigger、priority、condition、additionalCondition、invokeTargetCondition。</summary>
    public static AbilityDefinition Heal(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        ValueKey = nameof(ItemTemplate.Heal),
        ApplyCritMultiplier = true,
        Apply = Effect.HealApply,
        Priority = priority ?? AbilityPriority.Medium,
    };

    /// <summary>造成灼烧（Effect.BurnApply）。默认触发器 UseItem；可选 trigger、priority、condition、additionalCondition、invokeTargetCondition。</summary>
    public static AbilityDefinition Burn(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        ValueKey = nameof(ItemTemplate.Burn),
        ApplyCritMultiplier = true,
        Apply = Effect.BurnApply,
        Priority = priority ?? AbilityPriority.Medium,
    };

    /// <summary>造成剧毒（Effect.PoisonApply）。默认触发器 UseItem；可选 trigger、priority、condition、additionalCondition、invokeTargetCondition。</summary>
    public static AbilityDefinition Poison(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        ValueKey = nameof(ItemTemplate.Poison),
        ApplyCritMultiplier = true,
        Apply = Effect.PoisonApply,
        Priority = priority ?? AbilityPriority.Medium,
    };

    private static Condition WithCooldownTarget(Condition baseCondition) => baseCondition & Condition.NotDestroyed & Condition.HasCooldown;

    /// <summary>加速（Effect.HasteApply）。默认触发器 UseItem；目标默认己方(SameSide)、未摧毁且有冷却；targetCondition 代替默认，additionalTargetCondition 在 SameSide 基础上追加。</summary>
    public static AbilityDefinition Haste(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null, Condition? targetCondition = null, Condition? additionalTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        ApplyCritMultiplier = false,
        Apply = Effect.HasteApply,
        Priority = priority ?? AbilityPriority.Medium,
        TargetCondition = WithCooldownTarget(targetCondition ?? (additionalTargetCondition != null ? (Condition.SameSide & additionalTargetCondition) : Condition.SameSide)),
    };

    /// <summary>减速（Effect.SlowApply）。默认触发器 UseItem；目标默认敌方(DifferentSide)、未摧毁且有冷却；targetCondition 代替默认，additionalTargetCondition 在 DifferentSide 基础上追加。</summary>
    public static AbilityDefinition Slow(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null, Condition? targetCondition = null, Condition? additionalTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        ApplyCritMultiplier = false,
        Apply = Effect.SlowApply,
        Priority = priority ?? AbilityPriority.Medium,
        TargetCondition = WithCooldownTarget(targetCondition ?? (additionalTargetCondition != null ? (Condition.DifferentSide & additionalTargetCondition) : Condition.DifferentSide)),
    };

    /// <summary>冻结（Effect.FreezeApply）。默认触发器 UseItem；目标默认敌方(DifferentSide)、未摧毁且有冷却；targetCondition 代替默认，additionalTargetCondition 在 DifferentSide 基础上追加。</summary>
    public static AbilityDefinition Freeze(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null, Condition? targetCondition = null, Condition? additionalTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        ApplyCritMultiplier = false,
        Apply = Effect.FreezeApply,
        Priority = priority ?? AbilityPriority.Medium,
        TargetCondition = WithCooldownTarget(targetCondition ?? (additionalTargetCondition != null ? (Condition.DifferentSide & additionalTargetCondition) : Condition.DifferentSide)),
    };

    /// <summary>对己方满足目标条件的物品增加指定属性（限本场战斗）。attributeName 如 Damage、Poison；amountKey 默认 Custom_0。目标默认己方(SameSide)；targetCondition 完全代替默认，additionalTargetCondition 在 SameSide 基础上追加。可选 trigger、priority、condition 等。</summary>
    public static AbilityDefinition AddAttribute(string attributeName, string? amountKey = null, Condition? targetCondition = null, Condition? additionalTargetCondition = null, string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        ValueKey = amountKey ?? nameof(ItemTemplate.Custom_0),
        ApplyCritMultiplier = false,
        Apply = Effect.AddAttributeApply(attributeName),
        Priority = priority ?? AbilityPriority.Medium,
        TargetCondition = targetCondition ?? (additionalTargetCondition != null ? (Condition.SameSide & additionalTargetCondition) : Condition.SameSide),
    };

    /// <summary>对敌方满足目标条件的物品减少指定属性（限本场战斗，不低于 0）。amountKey 默认 Custom_0。目标默认敌方(DifferentSide)；targetCondition 完全代替默认，additionalTargetCondition 在 DifferentSide 基础上追加。可选 trigger、priority、condition 等。</summary>
    public static AbilityDefinition ReduceAttribute(string attributeName, string? amountKey = null, Condition? targetCondition = null, Condition? additionalTargetCondition = null, string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        ValueKey = amountKey ?? nameof(ItemTemplate.Custom_0),
        ApplyCritMultiplier = false,
        Apply = Effect.ReduceAttributeApply(attributeName),
        Priority = priority ?? AbilityPriority.Medium,
        TargetCondition = targetCondition ?? (additionalTargetCondition != null ? (Condition.DifferentSide & additionalTargetCondition) : Condition.DifferentSide),
    };
}
