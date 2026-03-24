namespace BazaarArena.Core;

public static class Apply
{
    public static readonly Action<BattleContext, AbilityDefinition> Damage = (ctx, ability) =>
    {
        int value = ctx.GetResolvedValue(Key.Damage, applyCritMultiplier: true, fallbackValue: ability.Value);
        int actualHp = ctx.ApplyDamageToOpp(value, isBurn: false);
        bool lifeSteal = ctx.GetResolvedValue(Key.LifeSteal, defaultValue: 0) != 0;
        if (lifeSteal && actualHp > 0) ctx.HealCaster(actualHp);
        ctx.LogEffect(lifeSteal ? "吸血" : "伤害", value, showCrit: ctx.IsCritNow);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Burn = (ctx, ability) =>
    {
        int value = ctx.GetResolvedValue(Key.Burn, applyCritMultiplier: true, fallbackValue: ability.Value);
        ctx.AddBurnToOpp(value);
        ctx.LogEffect("灼烧", value, showCrit: ctx.IsCritNow);
        ctx.ReportTriggerCause(Trigger.Burn);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Poison = (ctx, ability) =>
    {
        int value = ctx.GetResolvedValue(Key.Poison, applyCritMultiplier: true, fallbackValue: ability.Value);
        ctx.AddPoisonToOpp(value);
        ctx.LogEffect("剧毒", value, showCrit: ctx.IsCritNow);
        ctx.ReportTriggerCause(Trigger.Poison);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Shield = (ctx, ability) =>
    {
        int value = ctx.GetResolvedValue(Key.Shield, applyCritMultiplier: true, fallbackValue: ability.Value);
        ctx.AddShieldToCaster(value);
        ctx.LogEffect("护盾", value, showCrit: ctx.IsCritNow);
        ctx.ReportTriggerCause(Trigger.Shield);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Heal = (ctx, ability) =>
    {
        int requested = ctx.GetResolvedValue(Key.Heal, applyCritMultiplier: true, fallbackValue: ability.Value);
        ctx.HealCasterWithDebuffClear(requested);
        ctx.LogEffect("治疗", requested, showCrit: ctx.IsCritNow);
    };

    public static readonly Action<BattleContext, AbilityDefinition> GainGold = (ctx, ability) =>
    {
        int value = ctx.GetResolvedValue(Key.Gold, applyCritMultiplier: false, fallbackValue: ability.Value);
        ctx.AddGoldToCaster(value);
        ctx.LogEffect("金币", value, showCrit: false);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Charge = (ctx, ability) =>
    {
        int chargeMs = ctx.GetResolvedValue(Key.Charge, fallbackValue: ability.Value);
        int countKey = ability.TargetCountKey ?? Key.ChargeTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyCharge(chargeMs, count, ability.TargetCondition);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Freeze = (ctx, ability) =>
    {
        int freezeMs = ctx.GetResolvedValue(Key.Freeze, fallbackValue: ability.Value);
        int countKey = ability.TargetCountKey ?? Key.FreezeTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyFreeze(freezeMs, count, ability.TargetCondition);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Slow = (ctx, ability) =>
    {
        int slowMs = ctx.GetResolvedValue(Key.Slow, fallbackValue: ability.Value);
        int countKey = ability.TargetCountKey ?? Key.SlowTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplySlow(slowMs, count, ability.TargetCondition);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Haste = (ctx, ability) =>
    {
        int hasteMs = ctx.GetResolvedValue(Key.Haste, fallbackValue: ability.Value);
        int countKey = ability.TargetCountKey ?? Key.HasteTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyHaste(hasteMs, count, ability.TargetCondition, ability.EffectLogName);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Reload = (ctx, ability) =>
    {
        int amount = ctx.GetResolvedValue(Key.Custom_0, fallbackValue: ability.Value);
        int countKey = ability.TargetCountKey ?? Key.ReloadTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyReload(amount, count, ability.TargetCondition, ability.EffectLogName);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Repair = (ctx, ability) =>
    {
        int countKey = ability.TargetCountKey ?? Key.RepairTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyRepair(count, ability.TargetCondition);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Destroy = (ctx, ability) =>
    {
        int countKey = ability.TargetCountKey ?? Key.DestroyTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyDestroy(count, ability.TargetCondition);
    };

    public static Action<BattleContext, AbilityDefinition> AddAttribute(int attributeKey) =>
        (ctx, ability) =>
        {
            var targetCond = ability.TargetCondition ?? Condition.SameSide;
            int countKey = ability.TargetCountKey ?? Key.ModifyAttributeTargetCount;
            int cap = ctx.GetResolvedValue(countKey, defaultValue: 20);
            int maxTarget = cap >= 20 ? 0 : cap;
            int value = ctx.GetResolvedValue(ability.ValueKey ?? Key.Custom_0, applyCritMultiplier: ability.ApplyCritMultiplier, fallbackValue: ability.Value);
            ctx.AddAttributeToCasterSide(attributeKey, value, targetCond, maxTarget, ability.EffectLogName);
        };

    public static Action<BattleContext, AbilityDefinition> ReduceAttribute(int attributeKey) =>
        (ctx, ability) =>
        {
            var targetCond = ability.TargetCondition ?? Condition.DifferentSide;
            int countKey = ability.TargetCountKey ?? Key.ModifyAttributeTargetCount;
            int cap = ctx.GetResolvedValue(countKey, defaultValue: 20);
            int maxTarget = cap >= 20 ? 0 : cap;
            int value = ctx.GetResolvedValue(ability.ValueKey ?? Key.Custom_0, applyCritMultiplier: ability.ApplyCritMultiplier, fallbackValue: ability.Value);
            ctx.ReduceAttributeToSide(attributeKey, value, targetCond, maxTarget, ability.EffectLogName);
        };

    public static readonly Action<BattleContext, AbilityDefinition> StopFlying = (ctx, ability) =>
    {
        ctx.SetAttributeOnCasterSide(Key.InFlight, 0, ability.TargetCondition, ability.EffectLogName);
    };
}
