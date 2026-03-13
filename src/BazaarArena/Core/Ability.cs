namespace BazaarArena.Core;

/// <summary>常用能力定义的默认对象与工厂方法，用于简化物品定义；定制通过 Ability.xxx.Override(...) 链式调用。</summary>
public static class Ability
{
    private static Condition WithCooldownTarget(Condition baseCondition) => baseCondition & Condition.NotDestroyed & Condition.HasCooldown;
    private static Condition WithNotDestroyedTarget(Condition baseCondition) => baseCondition & Condition.NotDestroyed;

    /// <summary>造成伤害（Effect.DamageApply）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Damage => new()
    {
        TriggerName = Trigger.UseItem,
        Condition = Condition.SameAsSource,
        ValueKey = Key.Damage,
        ApplyCritMultiplier = true,
        Apply = Effect.DamageApply,
        Priority = AbilityPriority.Medium,
    };

    /// <summary>获得护盾（Effect.ShieldApply）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Shield => new()
    {
        TriggerName = Trigger.UseItem,
        Condition = Condition.SameAsSource,
        ValueKey = Key.Shield,
        ApplyCritMultiplier = true,
        Apply = Effect.ShieldApply,
        Priority = AbilityPriority.Medium,
    };

    /// <summary>治疗（Effect.HealApply）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Heal => new()
    {
        TriggerName = Trigger.UseItem,
        Condition = Condition.SameAsSource,
        ValueKey = Key.Heal,
        ApplyCritMultiplier = true,
        Apply = Effect.HealApply,
        Priority = AbilityPriority.Medium,
    };

    /// <summary>造成灼烧（Effect.BurnApply）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Burn => new()
    {
        TriggerName = Trigger.UseItem,
        Condition = Condition.SameAsSource,
        ValueKey = Key.Burn,
        ApplyCritMultiplier = true,
        Apply = Effect.BurnApply,
        Priority = AbilityPriority.Medium,
    };

    /// <summary>造成剧毒（Effect.PoisonApply）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Poison => new()
    {
        TriggerName = Trigger.UseItem,
        Condition = Condition.SameAsSource,
        ValueKey = Key.Poison,
        ApplyCritMultiplier = true,
        Apply = Effect.PoisonApply,
        Priority = AbilityPriority.Medium,
    };

    /// <summary>充能（Effect.ChargeApply）。默认触发器 UseItem；目标默认己方、未摧毁且有冷却；定制用 .Override(...)。</summary>
    public static AbilityDefinition Charge => new()
    {
        TriggerName = Trigger.UseItem,
        Condition = Condition.SameAsSource,
        ApplyCritMultiplier = false,
        Apply = Effect.ChargeApply,
        Priority = AbilityPriority.Medium,
        TargetCondition = WithCooldownTarget(Condition.SameSide),
    };

    /// <summary>加速（Effect.HasteApply）。默认触发器 UseItem；目标默认己方、未摧毁且有冷却；定制用 .Override(...)。</summary>
    public static AbilityDefinition Haste => new()
    {
        TriggerName = Trigger.UseItem,
        Condition = Condition.SameAsSource,
        ApplyCritMultiplier = false,
        Apply = Effect.HasteApply,
        Priority = AbilityPriority.Medium,
        TargetCondition = WithCooldownTarget(Condition.SameSide),
    };

    /// <summary>减速（Effect.SlowApply）。默认触发器 UseItem；目标默认敌方、未摧毁且有冷却；定制用 .Override(...)。</summary>
    public static AbilityDefinition Slow => new()
    {
        TriggerName = Trigger.UseItem,
        Condition = Condition.SameAsSource,
        ApplyCritMultiplier = false,
        Apply = Effect.SlowApply,
        Priority = AbilityPriority.Medium,
        TargetCondition = WithCooldownTarget(Condition.DifferentSide),
    };

    /// <summary>冻结（Effect.FreezeApply）。默认触发器 UseItem；目标默认敌方、未摧毁且有冷却；定制用 .Override(...)。</summary>
    public static AbilityDefinition Freeze => new()
    {
        TriggerName = Trigger.UseItem,
        Condition = Condition.SameAsSource,
        ApplyCritMultiplier = false,
        Apply = Effect.FreezeApply,
        Priority = AbilityPriority.Medium,
        TargetCondition = WithCooldownTarget(Condition.DifferentSide),
    };

    /// <summary>开始飞行：对己方满足目标条件且未飞行的物品设为飞行（等价于 AddAttribute(Key.InFlight) 设 1）。默认 additionalTargetCondition 为 NotInFlight；定制用 .Override(...)。</summary>
    public static AbilityDefinition StartFlying => AddAttribute(Key.InFlight).Override(value: 1, additionalTargetCondition: Condition.NotInFlight);

    /// <summary>结束飞行（Effect.StopFlyingApply）。默认触发器 UseItem；目标默认己方且处于飞行状态；定制用 .Override(...)。</summary>
    public static AbilityDefinition StopFlying => new()
    {
        TriggerName = Trigger.UseItem,
        Condition = Condition.SameAsSource,
        ApplyCritMultiplier = false,
        Apply = Effect.StopFlyingApply,
        Priority = AbilityPriority.Medium,
        TargetCondition = Condition.SameSide & Condition.InFlight,
    };

    /// <summary>摧毁（Effect.DestroyApply）。默认触发器 UseItem；目标默认己方、未摧毁；定制用 .Override(...)。</summary>
    public static AbilityDefinition Destroy => new()
    {
        TriggerName = Trigger.UseItem,
        Condition = Condition.SameAsSource,
        ApplyCritMultiplier = false,
        Apply = Effect.DestroyApply,
        Priority = AbilityPriority.Medium,
        TargetCondition = WithNotDestroyedTarget(Condition.SameSide),
    };

    /// <summary>修复（Effect.RepairApply）。默认触发器 UseItem；目标默认己方（实现内与 Condition.Destroyed 组合）；定制用 .Override(...)。</summary>
    public static AbilityDefinition Repair => new()
    {
        TriggerName = Trigger.UseItem,
        Condition = Condition.SameAsSource,
        ApplyCritMultiplier = false,
        Apply = Effect.RepairApply,
        Priority = AbilityPriority.Medium,
        TargetCondition = Condition.SameSide,
    };

    /// <summary>对己方满足目标条件的物品增加指定属性（限本场战斗）。attributeName 如 Key.Damage、Key.Poison；amountKey 默认 Key.Custom_0。目标默认己方；定制用 .Override(...)。</summary>
    public static AbilityDefinition AddAttribute(string attributeName, string? amountKey = null) => new()
    {
        TriggerName = Trigger.UseItem,
        Condition = Condition.SameAsSource,
        ValueKey = amountKey ?? Key.Custom_0,
        ApplyCritMultiplier = false,
        Apply = Effect.AddAttributeApply(attributeName),
        Priority = AbilityPriority.Medium,
        TargetCondition = Condition.SameSide,
    };

    /// <summary>对敌方满足目标条件的物品减少指定属性（限本场战斗，不低于 0）。amountKey 默认 Key.Custom_0。目标默认敌方；定制用 .Override(...)。</summary>
    public static AbilityDefinition ReduceAttribute(string attributeName, string? amountKey = null) => new()
    {
        TriggerName = Trigger.UseItem,
        Condition = Condition.SameAsSource,
        ValueKey = amountKey ?? Key.Custom_0,
        ApplyCritMultiplier = false,
        Apply = Effect.ReduceAttributeApply(attributeName),
        Priority = AbilityPriority.Medium,
        TargetCondition = Condition.DifferentSide,
    };
}
