namespace BazaarArena.Core;

/// <summary>常用能力定义的默认对象与工厂方法，用于简化物品定义；定制通过 Ability.xxx.Override(...) 链式调用。</summary>
public static class Ability
{
    private static Formula WithCooldownTarget(Formula baseCondition) => baseCondition & ~Condition.Destroyed & Condition.HasCooldown;
    private static Formula WithNotDestroyedTarget(Formula baseCondition) => baseCondition & ~Condition.Destroyed;
    private static Formula WithAmmoTarget(Formula baseCondition) => baseCondition & ~Condition.Destroyed & Condition.WithDerivedTag(DerivedTag.Ammo);

    private static AbilityDefinition CreateBase(AbilityType abilityType, Action<BattleContext, AbilityDefinition>? apply)
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
                    Condition = Condition.SameAsCaster,
                }
            ],
        };
    }

    /// <summary>造成伤害（Apply.Damage）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Damage => CreateBase(AbilityType.Damage, Core.Apply.Damage).Override(valueKey: Key.Damage, applyCritMultiplier: true);

    /// <summary>获得护盾（Apply.Shield）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Shield => CreateBase(AbilityType.Shield, Core.Apply.Shield).Override(valueKey: Key.Shield, applyCritMultiplier: true);

    /// <summary>治疗（Apply.Heal）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Heal => CreateBase(AbilityType.Heal, Core.Apply.Heal).Override(valueKey: Key.Heal, applyCritMultiplier: true);

    /// <summary>己方阵营生命再生提高（Apply.Regen）。数值来自模板 <see cref="ItemTemplate.Regen"/> / <see cref="Key.Regen"/>；默认触发器 UseItem。</summary>
    public static AbilityDefinition Regen => CreateBase(AbilityType.Regen, Core.Apply.Regen).Override(valueKey: Key.Regen, applyCritMultiplier: true);

    /// <summary>造成灼烧（Apply.Burn）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Burn => CreateBase(AbilityType.Burn, Core.Apply.Burn).Override(valueKey: Key.Burn, applyCritMultiplier: true);

    /// <summary>造成剧毒（Apply.Poison）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition Poison => CreateBase(AbilityType.Poison, Core.Apply.Poison).Override(valueKey: Key.Poison, applyCritMultiplier: true);

    /// <summary>对自身施加剧毒（Apply.PoisonSelf）。默认触发器 UseItem；定制用 .Override(...)。</summary>
    public static AbilityDefinition PoisonSelf => CreateBase(AbilityType.Poison, Core.Apply.PoisonSelf).Override(valueKey: Key.Poison, applyCritMultiplier: true);

    /// <summary>获取金币（Apply.GainGold）。默认触发器 UseItem；数值来自模板的 Gold，不参与暴击。</summary>
    public static AbilityDefinition GainGold => CreateBase(AbilityType.GainGold, Core.Apply.GainGold).Override(valueKey: Key.Gold, applyCritMultiplier: false);

    /// <summary>充能（Apply.Charge）。默认触发器 UseItem；目标默认己方、未摧毁且有冷却；定制用 .Override(...)。</summary>
    public static AbilityDefinition Charge => CreateBase(AbilityType.Charge, Core.Apply.Charge).Override(
        valueKey: Key.Charge,
        applyCritMultiplier: false,
        targetCondition: WithCooldownTarget(Condition.SameSide),
        targetCountKey: Key.ChargeTargetCount);

    /// <summary>加速（Apply.Haste）。默认触发器 UseItem；目标默认己方、未摧毁且有冷却；定制用 .Override(...)。</summary>
    public static AbilityDefinition Haste => CreateBase(AbilityType.Haste, Core.Apply.Haste).Override(
        valueKey: Key.Haste,
        applyCritMultiplier: false,
        targetCondition: WithCooldownTarget(Condition.SameSide),
        targetCountKey: Key.HasteTargetCount);

    /// <summary>装填弹药（Apply.Reload）。默认触发器 UseItem；目标默认己方、未摧毁且为弹药物品；数值取自 ValueKey（默认 Reload）。定制用 .Override(...)。</summary>
    public static AbilityDefinition Reload => CreateBase(AbilityType.Reload, Core.Apply.Reload).Override(
        valueKey: Key.Reload,
        applyCritMultiplier: false,
        targetCondition: WithAmmoTarget(Condition.SameSide),
        targetCountKey: Key.ReloadTargetCount);

    /// <summary>减速（Apply.Slow）。默认触发器 UseItem；目标默认敌方、未摧毁且有冷却；定制用 .Override(...)。</summary>
    public static AbilityDefinition Slow => CreateBase(AbilityType.Slow, Core.Apply.Slow).Override(
        valueKey: Key.Slow,
        applyCritMultiplier: false,
        targetCondition: WithCooldownTarget(Condition.DifferentSide),
        targetCountKey: Key.SlowTargetCount);

    /// <summary>冻结（Apply.Freeze）。默认触发器 UseItem；目标默认敌方、未摧毁且有冷却；定制用 .Override(...)。</summary>
    public static AbilityDefinition Freeze => CreateBase(AbilityType.Freeze, Core.Apply.Freeze).Override(
        valueKey: Key.Freeze,
        applyCritMultiplier: false,
        targetCondition: WithCooldownTarget(Condition.DifferentSide),
        targetCountKey: Key.FreezeTargetCount);

    /// <summary>开始飞行：对己方满足目标条件且未飞行的物品设为飞行。Apply 从 <see cref="Key.Custom_0"/> 读施加值，须 &gt;0（<see cref="Apply.AddAttribute"/> 在 value≤0 时不执行）；未单独作他用时可设 <c>Custom_0 = 1</c>。默认 additionalTargetCondition 为 NotInFlight；日志「开始飞行」。</summary>
    public static AbilityDefinition StartFlying => AddAttribute(Key.InFlight).Override(valueKey: Key.Custom_0, additionalTargetCondition: ~Condition.InFlight, effectLogName: "开始飞行");

    /// <summary>摧毁（Apply.Destroy）。默认触发器 UseItem；目标默认敌方、未摧毁；摧毁己方或相邻等需 .Override(targetCondition: ...) 显式指定。</summary>
    public static AbilityDefinition Destroy => CreateBase(AbilityType.Destroy, Core.Apply.Destroy).Override(
        applyCritMultiplier: false,
        targetCondition: WithNotDestroyedTarget(Condition.DifferentSide),
        targetCountKey: Key.DestroyTargetCount);

    /// <summary>修复（Apply.Repair）。默认触发器 UseItem；目标默认己方（实现内与 Condition.Destroyed 组合）；定制用 .Override(...)。</summary>
    public static AbilityDefinition Repair => CreateBase(AbilityType.Repair, Core.Apply.Repair).Override(
        applyCritMultiplier: false,
        targetCondition: Condition.SameSide,
        targetCountKey: Key.RepairTargetCount);

    /// <summary>结束飞行：对己方满足目标条件且处于飞行状态的物品取消飞行。</summary>
    public static AbilityDefinition StopFlying => ReduceAttribute(Key.InFlight, Key.Custom_0).Override(
        valueKey: Key.Custom_0,
        targetCondition: Condition.SameSide & Condition.InFlight,
        effectLogName: "结束飞行");

    /// <summary>获得无敌（Apply.Invincible）。持续时间来自 <see cref="ItemTemplate.Invincible"/>（内部写入 Key.Custom_3 毫秒）。默认触发器 UseItem。</summary>
    public static AbilityDefinition Invincible => CreateBase(AbilityType.Invincible, Core.Apply.Invincible).Override(
        valueKey: Key.InvincibleMs,
        applyCritMultiplier: false);

    /// <summary>使用此物品：将此物品直接加入施放队列，不消耗充能（绕过冷却）。常用于「使用其他某类物品时，使用此物品」。</summary>
    public static AbilityDefinition UseThisItem => CreateBase(AbilityType.UseThisItem, Core.Apply.UseThisItem).Override(
        applyCritMultiplier: false);

    /// <summary>解除己方无敌（Apply.ClearInvincible）。默认 UseItem；常与 BattleStart 无敌组合使用。</summary>
    public static AbilityDefinition ClearInvincible => CreateBase(AbilityType.ClearInvincible, Core.Apply.ClearInvincible).Override(
        applyCritMultiplier: false);

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
