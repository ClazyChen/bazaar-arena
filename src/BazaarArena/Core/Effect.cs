namespace BazaarArena.Core;

/// <summary>预定义效果应用委托：只读效果在委托内用 ctx.GetResolvedValue(key) 按常量 key 取值，不设 ValueKey；指定了 ValueKey 的能力（如 AddAttribute、ReduceAttribute）由模拟器填入 ctx.Value。</summary>
public static class Effect
{
    /// <summary>模板中生命再生字段的 key（ItemTemplate 无 Regen 属性，用常量避免魔法字符串）。</summary>
    private const string KeyRegen = "Regen";

    /// <summary>造成伤害：数值来自模板的 Damage；吸血时治疗己方并显示「吸血」。</summary>
    public static readonly Action<IEffectApplyContext> DamageApply = ctx =>
    {
        int value = ctx.GetResolvedValue(Key.Damage, applyCritMultiplier: true);
        int actualHp = ctx.ApplyDamageToOpp(value, isBurn: false);
        bool lifeSteal = ctx.GetResolvedValue(Key.LifeSteal, defaultValue: 0) != 0;
        if (lifeSteal && actualHp > 0) ctx.HealCaster(actualHp);
        ctx.LogEffect(lifeSteal ? "吸血" : "伤害", value, showCrit: ctx.IsCrit);
    };

    /// <summary>灼烧：数值来自模板的 Burn。</summary>
    public static readonly Action<IEffectApplyContext> BurnApply = ctx =>
    {
        int value = ctx.GetResolvedValue(Key.Burn, applyCritMultiplier: true);
        ctx.AddBurnToOpp(value);
        ctx.LogEffect("灼烧", value, showCrit: ctx.IsCrit);
    };

    /// <summary>剧毒：数值来自模板的 Poison。</summary>
    public static readonly Action<IEffectApplyContext> PoisonApply = ctx =>
    {
        int value = ctx.GetResolvedValue(Key.Poison, applyCritMultiplier: true);
        ctx.AddPoisonToOpp(value);
        ctx.LogEffect("剧毒", value, showCrit: ctx.IsCrit);
    };

    /// <summary>护盾：数值来自模板的 Shield。</summary>
    public static readonly Action<IEffectApplyContext> ShieldApply = ctx =>
    {
        int value = ctx.GetResolvedValue(Key.Shield, applyCritMultiplier: true);
        ctx.AddShieldToCaster(value);
        ctx.LogEffect("护盾", value, showCrit: ctx.IsCrit);
    };

    /// <summary>治疗：数值来自模板的 Heal；治疗时清除 5% 灼烧/剧毒。</summary>
    public static readonly Action<IEffectApplyContext> HealApply = ctx =>
    {
        int value = ctx.GetResolvedValue(Key.Heal, applyCritMultiplier: true);
        int actual = ctx.HealCasterWithDebuffClear(value);
        ctx.LogEffect("治疗", actual, showCrit: ctx.IsCrit);
    };

    /// <summary>生命再生：数值来自模板的 Regen。</summary>
    public static readonly Action<IEffectApplyContext> RegenApply = ctx =>
    {
        int value = ctx.GetResolvedValue(KeyRegen, applyCritMultiplier: true);
        ctx.AddRegenToCaster(value);
        ctx.LogEffect("生命再生", value, showCrit: ctx.IsCrit);
    };

    /// <summary>充能：根据 ChargeTargetCount 与 Charge（毫秒）选取满足能力 TargetCondition 的己方物品施加充能（默认未摧毁且有冷却）；不参与暴击。</summary>
    public static readonly Action<IEffectApplyContext> ChargeApply = ctx =>
    {
        int chargeMs = ctx.GetResolvedValue(Key.Charge);
        int count = ctx.GetResolvedValue(Key.ChargeTargetCount, defaultValue: 1);
        ctx.ApplyCharge(chargeMs, count, ctx.TargetCondition);
    };

    /// <summary>冻结：根据 FreezeTargetCount 与 Freeze（毫秒）选取满足能力 TargetCondition 的敌人物品施加冻结（默认未摧毁且有冷却）；触发次数按实际目标数。</summary>
    public static readonly Action<IEffectApplyContext> FreezeApply = ctx =>
    {
        int freezeMs = ctx.GetResolvedValue(Key.Freeze);
        int count = ctx.GetResolvedValue(Key.FreezeTargetCount, defaultValue: 1);
        ctx.ApplyFreeze(freezeMs, count, ctx.TargetCondition);
    };

    /// <summary>减速：根据 SlowTargetCount 与 Slow（毫秒）选取满足能力 TargetCondition 的敌人物品施加减速（默认未摧毁且有冷却）。</summary>
    public static readonly Action<IEffectApplyContext> SlowApply = ctx =>
    {
        int slowMs = ctx.GetResolvedValue(Key.Slow);
        int count = ctx.GetResolvedValue(Key.SlowTargetCount, defaultValue: 1);
        ctx.ApplySlow(slowMs, count, ctx.TargetCondition);
    };

    /// <summary>对己方满足能力 TargetCondition 的物品增加指定属性（限本场战斗）。attributeName 为要增加的属性名（如 Damage、Poison）；目标条件由能力 TargetCondition 注入，默认 SameSide。</summary>
    public static Action<IEffectApplyContext> AddAttributeApply(string attributeName) =>
        ctx => ctx.AddAttributeToCasterSide(attributeName, ctx.Value, ctx.TargetCondition ?? Condition.SameSide);

    /// <summary>对敌方满足能力 TargetCondition 的物品减少指定属性（限本场战斗，不低于 0）。目标条件由能力 TargetCondition 注入，默认 DifferentSide。</summary>
    public static Action<IEffectApplyContext> ReduceAttributeApply(string attributeName) =>
        ctx => ctx.ReduceAttributeToOpponentSide(attributeName, ctx.Value, ctx.TargetCondition ?? Condition.DifferentSide);

    /// <summary>加速：根据 HasteTargetCount 与 Haste（毫秒）选取己方有冷却且满足能力 TargetCondition 的物品施加加速；未设 TargetCondition 时默认 SameSide。</summary>
    public static readonly Action<IEffectApplyContext> HasteApply = ctx =>
    {
        int hasteMs = ctx.GetResolvedValue(Key.Haste);
        int count = ctx.GetResolvedValue(Key.HasteTargetCount, defaultValue: 1);
        ctx.ApplyHaste(hasteMs, count, ctx.TargetCondition);
    };

    /// <summary>修复：根据 RepairTargetCount 与能力 TargetCondition 选取己方已摧毁物品进行修复（实现内会与 Condition.Destroyed 组合）；默认 SameSide。</summary>
    public static readonly Action<IEffectApplyContext> RepairApply = ctx =>
    {
        int count = ctx.GetResolvedValue(Key.RepairTargetCount, defaultValue: 1);
        ctx.ApplyRepair(count, ctx.TargetCondition);
    };

    /// <summary>结束飞行：对己方满足 TargetCondition 且处于飞行状态的物品设为未飞行。目标默认己方且 InFlight（见 Ability.StopFlying）。</summary>
    public static readonly Action<IEffectApplyContext> StopFlyingApply = ctx =>
    {
        ctx.SetAttributeOnCasterSide(Key.InFlight, 0, ctx.TargetCondition);
    };

    /// <summary>摧毁：根据 DestroyTargetCount 与能力 TargetCondition 选取己方未摧毁物品施加摧毁（默认 1 个）；先触发「摧毁物品时」，再标记 Destroyed。目标不要求有冷却。</summary>
    public static readonly Action<IEffectApplyContext> DestroyApply = ctx =>
    {
        int count = ctx.GetResolvedValue(Key.DestroyTargetCount, defaultValue: 1);
        ctx.ApplyDestroy(count, ctx.TargetCondition);
    };
}
