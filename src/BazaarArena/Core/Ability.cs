namespace BazaarArena.Core;

/// <summary>常用能力定义的工厂方法，用于简化物品定义。</summary>
public static class Ability
{
    private static Condition MergeCondition(string trigger, Condition? condition, Condition? additionalCondition)
    {
        var defaultCond = trigger == Trigger.UseItem ? Condition.SameAsSource : Condition.SameSide;
        var final = condition ?? defaultCond;
        return additionalCondition != null ? Condition.And(final, additionalCondition) : final;
    }

    /// <summary>造成伤害（Effect.Damage）。默认触发器 UseItem；可选 trigger、priority、condition、additionalCondition、invokeTargetCondition。</summary>
    public static AbilityDefinition Damage(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        Effects = [Effect.Damage],
        Priority = priority ?? AbilityPriority.Medium,
    };

    /// <summary>获得护盾（Effect.Shield）。默认触发器 UseItem；可选 trigger、priority、condition、additionalCondition、invokeTargetCondition。</summary>
    public static AbilityDefinition Shield(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        Effects = [Effect.Shield],
        Priority = priority ?? AbilityPriority.Medium,
    };

    /// <summary>治疗（Effect.Heal）。默认触发器 UseItem；可选 trigger、priority、condition、additionalCondition、invokeTargetCondition。</summary>
    public static AbilityDefinition Heal(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        Effects = [Effect.Heal],
        Priority = priority ?? AbilityPriority.Medium,
    };

    /// <summary>造成灼烧（Effect.Burn）。默认触发器 UseItem；可选 trigger、priority、condition、additionalCondition、invokeTargetCondition。</summary>
    public static AbilityDefinition Burn(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        Effects = [Effect.Burn],
        Priority = priority ?? AbilityPriority.Medium,
    };

    /// <summary>造成剧毒（Effect.Poison）。默认触发器 UseItem；可选 trigger、priority、condition、additionalCondition、invokeTargetCondition。</summary>
    public static AbilityDefinition Poison(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        Effects = [Effect.Poison],
        Priority = priority ?? AbilityPriority.Medium,
    };

    /// <summary>加速（Effect.Haste）。默认触发器 UseItem；目标默认己方(SameSide)。可选 trigger、priority、condition、additionalCondition、invokeTargetCondition；targetCondition 代替默认，additionalTargetCondition 在 SameSide 基础上追加。</summary>
    public static AbilityDefinition Haste(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null, Condition? targetCondition = null, Condition? additionalTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        Effects = [Effect.Haste],
        Priority = priority ?? AbilityPriority.Medium,
        TargetCondition = targetCondition ?? (additionalTargetCondition != null ? Condition.And(Condition.SameSide, additionalTargetCondition) : Condition.SameSide),
    };

    /// <summary>减速（Effect.Slow）。默认触发器 UseItem；目标默认敌方(DifferentSide)。可选 trigger、priority、condition、additionalCondition、invokeTargetCondition；targetCondition 代替默认，additionalTargetCondition 在 DifferentSide 基础上追加。</summary>
    public static AbilityDefinition Slow(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null, Condition? targetCondition = null, Condition? additionalTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        Effects = [Effect.Slow],
        Priority = priority ?? AbilityPriority.Medium,
        TargetCondition = targetCondition ?? (additionalTargetCondition != null ? Condition.And(Condition.DifferentSide, additionalTargetCondition) : Condition.DifferentSide),
    };

    /// <summary>冻结（Effect.Freeze）。默认触发器 UseItem；目标默认敌方(DifferentSide)。可选 trigger、priority、condition、additionalCondition、invokeTargetCondition；targetCondition 代替默认，additionalTargetCondition 在 DifferentSide 基础上追加。</summary>
    public static AbilityDefinition Freeze(string trigger = Trigger.UseItem, AbilityPriority? priority = null, Condition? condition = null, Condition? additionalCondition = null, Condition? invokeTargetCondition = null, Condition? targetCondition = null, Condition? additionalTargetCondition = null) => new()
    {
        TriggerName = trigger,
        Condition = MergeCondition(trigger, condition, additionalCondition),
        InvokeTargetCondition = invokeTargetCondition,
        Effects = [Effect.Freeze],
        Priority = priority ?? AbilityPriority.Medium,
        TargetCondition = targetCondition ?? (additionalTargetCondition != null ? Condition.And(Condition.DifferentSide, additionalTargetCondition) : Condition.DifferentSide),
    };
}
