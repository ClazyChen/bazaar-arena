namespace BazaarArena.Core;

public static class Apply
{
    public static readonly Action<BattleContext> Damage = ctx =>
    {
        int value = ctx.GetResolvedValue(Key.Damage, applyCritMultiplier: true);
        int actualHp = ctx.ApplyDamageToOpp(value, isBurn: false);
        bool lifeSteal = ctx.GetResolvedValue(Key.LifeSteal, defaultValue: 0) != 0;
        if (lifeSteal && actualHp > 0) ctx.HealCaster(actualHp);
        ctx.LogEffect(lifeSteal ? "吸血" : "伤害", value, showCrit: ctx.IsCrit);
    };

    public static readonly Action<BattleContext> Burn = ctx =>
    {
        int value = ctx.GetResolvedValue(Key.Burn, applyCritMultiplier: true);
        ctx.AddBurnToOpp(value);
        ctx.LogEffect("灼烧", value, showCrit: ctx.IsCrit);
        ctx.ReportTriggerCause(Trigger.Burn);
    };

    public static readonly Action<BattleContext> Poison = ctx =>
    {
        int value = ctx.GetResolvedValue(Key.Poison, applyCritMultiplier: true);
        ctx.AddPoisonToOpp(value);
        ctx.LogEffect("剧毒", value, showCrit: ctx.IsCrit);
        ctx.ReportTriggerCause(Trigger.Poison);
    };

    public static readonly Action<BattleContext> Shield = ctx =>
    {
        int value = ctx.GetResolvedValue(Key.Shield, applyCritMultiplier: true);
        ctx.AddShieldToCaster(value);
        ctx.LogEffect("护盾", value, showCrit: ctx.IsCrit);
        ctx.ReportTriggerCause(Trigger.Shield);
    };

    public static readonly Action<BattleContext> Heal = ctx =>
    {
        int requested = ctx.GetResolvedValue(Key.Heal, applyCritMultiplier: true);
        ctx.HealCasterWithDebuffClear(requested);
        ctx.LogEffect("治疗", requested, showCrit: ctx.IsCrit);
    };

    public static readonly Action<BattleContext> GainGold = ctx =>
    {
        int value = ctx.GetResolvedValue(Key.Gold, applyCritMultiplier: false);
        ctx.AddGoldToCaster(value);
        ctx.LogEffect("金币", value, showCrit: false);
    };

    public static readonly Action<BattleContext> Charge = ctx =>
    {
        int chargeMs = ctx.GetResolvedValue(Key.Charge);
        int countKey = ctx.TargetCountKey ?? Key.ChargeTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyCharge(chargeMs, count, ctx.TargetCondition);
    };

    public static readonly Action<BattleContext> Freeze = ctx =>
    {
        int freezeMs = ctx.GetResolvedValue(Key.Freeze);
        int countKey = ctx.TargetCountKey ?? Key.FreezeTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyFreeze(freezeMs, count, ctx.TargetCondition);
    };

    public static readonly Action<BattleContext> Slow = ctx =>
    {
        int slowMs = ctx.GetResolvedValue(Key.Slow);
        int countKey = ctx.TargetCountKey ?? Key.SlowTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplySlow(slowMs, count, ctx.TargetCondition);
    };

    public static readonly Action<BattleContext> Haste = ctx =>
    {
        int hasteMs = ctx.GetResolvedValue(Key.Haste);
        int countKey = ctx.TargetCountKey ?? Key.HasteTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyHaste(hasteMs, count, ctx.TargetCondition);
    };

    public static readonly Action<BattleContext> Reload = ctx =>
    {
        int amount = ctx.GetResolvedValue(Key.Custom_0);
        int countKey = ctx.TargetCountKey ?? Key.ReloadTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyReload(amount, count, ctx.TargetCondition);
    };

    public static readonly Action<BattleContext> Repair = ctx =>
    {
        int countKey = ctx.TargetCountKey ?? Key.RepairTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyRepair(count, ctx.TargetCondition);
    };

    public static readonly Action<BattleContext> Destroy = ctx =>
    {
        int countKey = ctx.TargetCountKey ?? Key.DestroyTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyDestroy(count, ctx.TargetCondition);
    };

    public static Action<BattleContext> AddAttribute(int attributeKey) =>
        ctx =>
        {
            var targetCond = ctx.TargetCondition ?? Condition.SameSide;
            int countKey = ctx.TargetCountKey ?? Key.ModifyAttributeTargetCount;
            int cap = ctx.GetResolvedValue(countKey, defaultValue: 20);
            int maxTarget = cap >= 20 ? 0 : cap;
            ctx.AddAttributeToCasterSide(attributeKey, ctx.ResolvedValue, targetCond, maxTarget);
        };

    public static Action<BattleContext> ReduceAttribute(int attributeKey) =>
        ctx =>
        {
            var targetCond = ctx.TargetCondition ?? Condition.DifferentSide;
            int countKey = ctx.TargetCountKey ?? Key.ModifyAttributeTargetCount;
            int cap = ctx.GetResolvedValue(countKey, defaultValue: 20);
            int maxTarget = cap >= 20 ? 0 : cap;
            ctx.ReduceAttributeToSide(attributeKey, ctx.ResolvedValue, targetCond, maxTarget, ctx.EffectLogName);
        };

    public static readonly Action<BattleContext> StopFlying = ctx =>
    {
        ctx.SetAttributeOnCasterSide(Key.InFlight, 0, ctx.TargetCondition);
    };
}
