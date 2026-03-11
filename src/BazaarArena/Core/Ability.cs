namespace BazaarArena.Core;

/// <summary>常用能力定义的工厂方法，用于简化物品定义。</summary>
public static class Ability
{
    /// <summary>使用物品时造成伤害（Trigger.UseItem + Effect.Damage）。可选 priority 覆盖默认 Medium。</summary>
    public static AbilityDefinition DamageOnUseItem(AbilityPriority? priority = null) => new()
    {
        TriggerName = Trigger.UseItem,
        Effects = [Effect.Damage],
        Priority = priority ?? AbilityPriority.Medium,
    };

    /// <summary>使用物品时获得护盾（Trigger.UseItem + Effect.Shield）。可选 priority 覆盖默认 Medium。</summary>
    public static AbilityDefinition ShieldOnUseItem(AbilityPriority? priority = null) => new()
    {
        TriggerName = Trigger.UseItem,
        Effects = [Effect.Shield],
        Priority = priority ?? AbilityPriority.Medium,
    };

    /// <summary>使用物品时治疗（Trigger.UseItem + Effect.Heal）。可选 priority 覆盖默认 Medium。</summary>
    public static AbilityDefinition HealOnUseItem(AbilityPriority? priority = null) => new()
    {
        TriggerName = Trigger.UseItem,
        Effects = [Effect.Heal],
        Priority = priority ?? AbilityPriority.Medium,
    };

    /// <summary>使用物品时造成灼烧（Trigger.UseItem + Effect.Burn）。可选 priority 覆盖默认 Medium。</summary>
    public static AbilityDefinition BurnOnUseItem(AbilityPriority? priority = null) => new()
    {
        TriggerName = Trigger.UseItem,
        Effects = [Effect.Burn],
        Priority = priority ?? AbilityPriority.Medium,
    };

    /// <summary>使用物品时造成剧毒（Trigger.UseItem + Effect.Poison）。可选 priority 覆盖默认 Medium。</summary>
    public static AbilityDefinition PoisonOnUseItem(AbilityPriority? priority = null) => new()
    {
        TriggerName = Trigger.UseItem,
        Effects = [Effect.Poison],
        Priority = priority ?? AbilityPriority.Medium,
    };

    /// <summary>使用物品时加速（Trigger.UseItem + Effect.Haste）。目标默认己方(SameSide)。可选 priority；targetCondition 代替默认，additionalTargetCondition 在 SameSide 基础上追加。</summary>
    public static AbilityDefinition HasteOnUseItem(AbilityPriority? priority = null, Condition? targetCondition = null, Condition? additionalTargetCondition = null) => new()
    {
        TriggerName = Trigger.UseItem,
        Effects = [Effect.Haste],
        Priority = priority ?? AbilityPriority.Medium,
        TargetCondition = targetCondition ?? (additionalTargetCondition != null ? Condition.And(Condition.SameSide, additionalTargetCondition) : Condition.SameSide),
    };

    /// <summary>使用物品时减速（Trigger.UseItem + Effect.Slow）。目标默认敌方(DifferentSide)。可选 priority；targetCondition 代替默认，additionalTargetCondition 在 DifferentSide 基础上追加。</summary>
    public static AbilityDefinition SlowOnUseItem(AbilityPriority? priority = null, Condition? targetCondition = null, Condition? additionalTargetCondition = null) => new()
    {
        TriggerName = Trigger.UseItem,
        Effects = [Effect.Slow],
        Priority = priority ?? AbilityPriority.Medium,
        TargetCondition = targetCondition ?? (additionalTargetCondition != null ? Condition.And(Condition.DifferentSide, additionalTargetCondition) : Condition.DifferentSide),
    };

    /// <summary>使用物品时冻结（Trigger.UseItem + Effect.Freeze）。目标默认敌方(DifferentSide)。可选 priority；targetCondition 代替默认，additionalTargetCondition 在 DifferentSide 基础上追加。</summary>
    public static AbilityDefinition FreezeOnUseItem(AbilityPriority? priority = null, Condition? targetCondition = null, Condition? additionalTargetCondition = null) => new()
    {
        TriggerName = Trigger.UseItem,
        Effects = [Effect.Freeze],
        Priority = priority ?? AbilityPriority.Medium,
        TargetCondition = targetCondition ?? (additionalTargetCondition != null ? Condition.And(Condition.DifferentSide, additionalTargetCondition) : Condition.DifferentSide),
    };
}
