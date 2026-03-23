namespace BazaarArena.Core;

/// <summary>常用能力定义的默认对象与工厂方法，用于简化物品定义；定制通过 Ability.xxx.Override(...) 链式调用。</summary>
public static class Ability
{
    private static Formula WithCooldownTarget(Formula baseCondition) => baseCondition & Condition.NotDestroyed & Condition.HasCooldown;
    private static Formula WithNotDestroyedTarget(Formula baseCondition) => baseCondition & Condition.NotDestroyed;
    private static Formula WithAmmoTarget(Formula baseCondition) => baseCondition & Condition.NotDestroyed & Condition.WithTag(DerivedTag.Ammo);

    private static AbilityDefinition CreateBase(AbilityType abilityType, Action<BattleContext>? apply)
    {
        return new AbilityDefinition
        {
            AbilityType = abilityType,
            Apply = apply,
            Priority = AbilityPriority.Medium,
            TriggerEntries =
            [
                new TriggerEntry
                {
                    Trigger = Trigger.UseItem,
                    Condition = Condition.SameAsSource,
                }
            ],
        };
    }

    /// <summary>造成伤害（Effect.DamageApply）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Damage => CreateBase(AbilityType.Damage, Core.Apply.Damage).Override(valueKey: Key.Damage, applyCritMultiplier: true);

    /// <summary>获得护盾（Effect.ShieldApply）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Shield => CreateBase(AbilityType.Shield, Core.Apply.Shield).Override(valueKey: Key.Shield, applyCritMultiplier: true);

    /// <summary>治疗（Effect.HealApply）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Heal => CreateBase(AbilityType.Heal, Core.Apply.Heal).Override(valueKey: Key.Heal, applyCritMultiplier: true);

    /// <summary>造成灼烧（Effect.BurnApply）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Burn => CreateBase(AbilityType.Burn, Core.Apply.Burn).Override(valueKey: Key.Burn, applyCritMultiplier: true);

    /// <summary>造成剧毒（Effect.PoisonApply）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Poison => CreateBase(AbilityType.Poison, Core.Apply.Poison).Override(valueKey: Key.Poison, applyCritMultiplier: true);

    /// <summary>对自身施加剧毒（过渡兼容，当前临时复用 Poison 行为）。</summary>
    public static AbilityDefinition PoisonSelf => Poison;

    /// <summary>获取金币（Effect.GainGoldApply）。默认触发器 UseItem；数值来自模板的 Gold，不参与暴击。</summary>
    public static AbilityDefinition GainGold => CreateBase(AbilityType.GainGold, Core.Apply.GainGold).Override(valueKey: Key.Gold, applyCritMultiplier: false);

    /// <summary>充能（Effect.ChargeApply）。默认触发器 UseItem；目标默认己方、未摧毁且有冷却；定制用 .Override(...)。</summary>
    public static AbilityDefinition Charge => CreateBase(AbilityType.Charge, Core.Apply.Charge).Override(
        applyCritMultiplier: false,
        targetCondition: WithCooldownTarget(Condition.SameSide),
        targetCountKey: Key.ChargeTargetCount);

    /// <summary>加速（Effect.HasteApply）。默认触发器 UseItem；目标默认己方、未摧毁且有冷却；定制用 .Override(...)。</summary>
    public static AbilityDefinition Haste => CreateBase(AbilityType.Haste, Core.Apply.Haste).Override(
        applyCritMultiplier: false,
        targetCondition: WithCooldownTarget(Condition.SameSide),
        targetCountKey: Key.HasteTargetCount);

    /// <summary>装填弹药（Effect.ReloadApply）。默认触发器 UseItem；目标默认己方、未摧毁且为弹药物品；数值取自 ValueKey（默认 Custom_0）。定制用 .Override(...)。</summary>
    public static AbilityDefinition Reload => CreateBase(AbilityType.Reload, Core.Apply.Reload).Override(
        valueKey: Key.Custom_0,
        applyCritMultiplier: false,
        targetCondition: WithAmmoTarget(Condition.SameSide),
        targetCountKey: Key.ReloadTargetCount);

    /// <summary>减速（Effect.SlowApply）。默认触发器 UseItem；目标默认敌方、未摧毁且有冷却；定制用 .Override(...)。</summary>
    public static AbilityDefinition Slow => CreateBase(AbilityType.Slow, Core.Apply.Slow).Override(
        applyCritMultiplier: false,
        targetCondition: WithCooldownTarget(Condition.DifferentSide),
        targetCountKey: Key.SlowTargetCount);

    /// <summary>冻结（Effect.FreezeApply）。默认触发器 UseItem；目标默认敌方、未摧毁且有冷却；定制用 .Override(...)。</summary>
    public static AbilityDefinition Freeze => CreateBase(AbilityType.Freeze, Core.Apply.Freeze).Override(
        applyCritMultiplier: false,
        targetCondition: WithCooldownTarget(Condition.DifferentSide),
        targetCountKey: Key.FreezeTargetCount);

    /// <summary>开始飞行：对己方满足目标条件且未飞行的物品设为飞行（等价于 AddAttribute(Key.InFlight) 设 1）。默认 additionalTargetCondition 为 NotInFlight；日志显示「开始飞行」。</summary>
    public static AbilityDefinition StartFlying => AddAttribute(Key.InFlight).Override(value: 1, additionalTargetCondition: Condition.NotInFlight, effectLogName: "开始飞行");

    /// <summary>摧毁（Effect.DestroyApply）。默认触发器 UseItem；目标默认己方、未摧毁；定制用 .Override(...)。</summary>
    public static AbilityDefinition Destroy => CreateBase(AbilityType.Destroy, Core.Apply.Destroy).Override(
        applyCritMultiplier: false,
        targetCondition: WithNotDestroyedTarget(Condition.SameSide),
        targetCountKey: Key.DestroyTargetCount);

    /// <summary>修复（Effect.RepairApply）。默认触发器 UseItem；目标默认己方（实现内与 Condition.Destroyed 组合）；定制用 .Override(...)。</summary>
    public static AbilityDefinition Repair => CreateBase(AbilityType.Repair, Core.Apply.Repair).Override(
        applyCritMultiplier: false,
        targetCondition: Condition.SameSide,
        targetCountKey: Key.RepairTargetCount);

    /// <summary>结束飞行：对己方满足目标条件且处于飞行状态的物品取消飞行。</summary>
    public static AbilityDefinition StopFlying => ReduceAttribute(Key.InFlight, Key.Custom_0).Override(
        value: 1,
        targetCondition: Condition.SameSide & Condition.InFlight,
        effectLogName: "结束飞行");

    /// <summary>对满足目标条件的物品增加指定属性（限本场战斗）。attributeKey 如 Key.Damage、Key.Poison；amountKey 默认 Key.Custom_0。</summary>
    public static AbilityDefinition AddAttribute(int attributeKey, int? amountKey = null) => CreateBase(AbilityType.AddAttribute, Apply.AddAttribute(attributeKey)).Override(
        valueKey: amountKey ?? Key.Custom_0,
        applyCritMultiplier: false,
        targetCondition: Condition.SameSide,
        targetCountKey: Key.ModifyAttributeTargetCount);

    /// <summary>对满足目标条件的物品减少指定属性（限本场战斗，不低于 0）。amountKey 默认 Key.Custom_0。</summary>
    public static AbilityDefinition ReduceAttribute(int attributeKey, int? amountKey = null) => CreateBase(AbilityType.ReduceAttribute, Apply.ReduceAttribute(attributeKey)).Override(
        valueKey: amountKey ?? Key.Custom_0,
        applyCritMultiplier: false,
        targetCondition: Condition.DifferentSide,
        targetCountKey: Key.ModifyAttributeTargetCount);
}
