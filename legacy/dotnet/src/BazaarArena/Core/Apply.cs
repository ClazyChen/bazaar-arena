namespace BazaarArena.Core;

public static class Apply
{
    private static int ReadCount(BattleContext ctx, int key)
    {
        int count = ctx.GetItemInt(ctx.Caster, key);
        return count > 0 ? count : 1;
    }

    public static readonly Action<BattleContext, AbilityDefinition> Damage = (ctx, ability) =>
    {
        int value = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
        if (ctx.IsCritNow) value *= ctx.CurrentCritMultiplier;
        int actualHp = ctx.ApplyDamageToOpp(value, isBurn: false);
        bool lifeSteal = ctx.GetItemInt(ctx.Caster, Key.LifeSteal) != 0;
        if (lifeSteal && actualHp > 0) ctx.HealCaster(actualHp);
        ctx.LogEffect(lifeSteal ? "吸血" : "伤害", value, showCrit: ctx.IsCritNow);
        if (actualHp > 0)
            ctx.BattleState.InvokeTrigger(Trigger.OppHpReduced, ctx.Caster, null, 1);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Burn = (ctx, ability) =>
    {
        int value = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
        if (ctx.IsCritNow) value *= ctx.CurrentCritMultiplier;
        ctx.AddBurnToOpp(value);
        ctx.LogEffect("灼烧", value, showCrit: ctx.IsCritNow);
        ctx.ReportTriggerCause(Trigger.Burn);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Poison = (ctx, ability) =>
    {
        int value = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
        if (ctx.IsCritNow) value *= ctx.CurrentCritMultiplier;
        ctx.AddPoisonToOpp(value);
        ctx.LogEffect("剧毒", value, showCrit: ctx.IsCritNow);
        ctx.ReportTriggerCause(Trigger.Poison);
    };

    public static readonly Action<BattleContext, AbilityDefinition> PoisonSelf = (ctx, ability) =>
    {
        int value = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
        if (ctx.IsCritNow) value *= ctx.CurrentCritMultiplier;
        ctx.AddPoisonToCaster(value);
        ctx.LogEffect("剧毒", value, showCrit: ctx.IsCritNow);
        ctx.ReportTriggerCause(Trigger.Poison);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Shield = (ctx, ability) =>
    {
        int value = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
        if (ctx.IsCritNow) value *= ctx.CurrentCritMultiplier;
        ctx.AddShieldToCaster(value);
        ctx.LogEffect("护盾", value, showCrit: ctx.IsCritNow);
        ctx.ReportTriggerCause(Trigger.Shield);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Heal = (ctx, ability) =>
    {
        int requested = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
        if (ctx.IsCritNow) requested *= ctx.CurrentCritMultiplier;
        int actual = ctx.HealCasterWithDebuffClear(requested);
        ctx.LogEffect("治疗", requested, showCrit: ctx.IsCritNow);
        if (actual > 0)
            ctx.ReportTriggerCause(Trigger.Heal);
    };

    public static readonly Action<BattleContext, AbilityDefinition> GainGold = (ctx, ability) =>
    {
        int value = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
        ctx.AddGoldToCaster(value);
        ctx.LogEffect("金币", value, showCrit: false);
    };

    public static readonly Action<BattleContext, AbilityDefinition> UseThisItem = (ctx, ability) =>
    {
        if (!ctx.AllowCastQueueEnqueue) return;
        if (ctx.Caster.Destroyed) return;
        if (ctx.Caster.FreezeRemainingMs > 0) return;
        int ammoCap = ctx.GetItemInt(ctx.Caster, Key.AmmoCap);
        if (ammoCap > 0 && ctx.Caster.AmmoRemaining <= 0) return;
        ctx.BattleState.CastQueue.Add(ctx.Caster);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Charge = (ctx, ability) =>
    {
        int chargeMs = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
        int countKey = ability.TargetCountKey ?? Key.ChargeTargetCount;
        int count = ReadCount(ctx, countKey);
        ctx.ApplyCharge(chargeMs, count, ability.TargetCondition, ability.EffectLogName);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Freeze = (ctx, ability) =>
    {
        int freezeMs = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
        int countKey = ability.TargetCountKey ?? Key.FreezeTargetCount;
        int count = ReadCount(ctx, countKey);
        ctx.ApplyFreeze(freezeMs, count, ability.TargetCondition);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Slow = (ctx, ability) =>
    {
        int slowMs = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
        int countKey = ability.TargetCountKey ?? Key.SlowTargetCount;
        int count = ReadCount(ctx, countKey);
        ctx.ApplySlow(slowMs, count, ability.TargetCondition);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Haste = (ctx, ability) =>
    {
        int hasteMs = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
        int countKey = ability.TargetCountKey ?? Key.HasteTargetCount;
        int count = ReadCount(ctx, countKey);
        ctx.ApplyHaste(hasteMs, count, ability.TargetCondition, ability.EffectLogName);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Reload = (ctx, ability) =>
    {
        int amount = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
        int countKey = ability.TargetCountKey ?? Key.ReloadTargetCount;
        int count = ReadCount(ctx, countKey);
        ctx.ApplyReload(amount, count, ability.TargetCondition, ability.EffectLogName);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Repair = (ctx, ability) =>
    {
        int countKey = ability.TargetCountKey ?? Key.RepairTargetCount;
        int count = ReadCount(ctx, countKey);
        ctx.ApplyRepair(count, ability.TargetCondition);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Destroy = (ctx, ability) =>
    {
        int countKey = ability.TargetCountKey ?? Key.DestroyTargetCount;
        int count = ReadCount(ctx, countKey);
        ctx.ApplyDestroy(count, ability.TargetCondition);
    };

    public static Action<BattleContext, AbilityDefinition> AddAttribute(int attributeKey) =>
        (ctx, ability) =>
        {
            var targetCond = ability.TargetCondition ?? Condition.SameSide;
            int countKey = ability.TargetCountKey ?? Key.ModifyAttributeTargetCount;
            int cap = ctx.GetItemInt(ctx.Caster, countKey);
            if (cap <= 0) cap = 20;
            int maxTarget = cap >= 20 ? 0 : cap;
            int value = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
            if (ability.ApplyCritMultiplier && ctx.IsCritNow) value *= ctx.CurrentCritMultiplier;
            ctx.AddAttributeToCasterSide(attributeKey, value, targetCond, maxTarget, ability.EffectLogName);
        };

    public static Action<BattleContext, AbilityDefinition> ReduceAttribute(int attributeKey) =>
        (ctx, ability) =>
        {
            var targetCond = ability.TargetCondition ?? Condition.DifferentSide;
            int countKey = ability.TargetCountKey ?? Key.ModifyAttributeTargetCount;
            int cap = ctx.GetItemInt(ctx.Caster, countKey);
            if (cap <= 0) cap = 20;
            int maxTarget = cap >= 20 ? 0 : cap;
            int value = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
            if (ability.ApplyCritMultiplier && ctx.IsCritNow) value *= ctx.CurrentCritMultiplier;
            ctx.ReduceAttributeToSide(attributeKey, value, targetCond, maxTarget, ability.EffectLogName);
        };

    public static readonly Action<BattleContext, AbilityDefinition> StopFlying = (ctx, ability) =>
    {
        ctx.SetAttributeOnCasterSide(Key.InFlight, 0, ability.TargetCondition, ability.EffectLogName);
    };

    public static readonly Action<BattleContext, AbilityDefinition> Invincible = (ctx, ability) =>
    {
        int durationMs = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
        if (durationMs <= 0) return;
        ctx.CurrentSide.InvincibleRemainingMs += durationMs;
        ctx.LogEffect("无敌", durationMs, showCrit: false);
    };

    /// <summary>将己方无敌剩余时间清零（不依赖数值 key）。</summary>
    public static readonly Action<BattleContext, AbilityDefinition> ClearInvincible = (ctx, _) =>
    {
        if (ctx.CurrentSide.InvincibleRemainingMs <= 0) return;
        ctx.CurrentSide.InvincibleRemainingMs = 0;
        ctx.LogEffect("解除无敌", 0, showCrit: false);
    };

    /// <summary>提高己方阵营生命再生；数值来自 <see cref="AbilityDefinition.ValueKey"/>（默认 <see cref="Key.Regen"/>）。</summary>
    public static readonly Action<BattleContext, AbilityDefinition> Regen = (ctx, ability) =>
    {
        int value = ctx.GetItemInt(ctx.Caster, ability.ValueKey!.Value);
        if (ability.ApplyCritMultiplier && ctx.IsCritNow) value *= ctx.CurrentCritMultiplier;
        if (value <= 0) return;
        ctx.CurrentSide.Regen += value;
        ctx.LogEffect("生命再生", value, showCrit: ability.ApplyCritMultiplier && ctx.IsCritNow);
        ctx.ReportTriggerCause(Trigger.Regen);
    };
}
