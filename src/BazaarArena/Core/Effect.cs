namespace BazaarArena.Core;

/// <summary>属性 key 对应的中文名，用于效果日志默认显示（如「护盾降低」「伤害提高」）；未收录时用「属性」。</summary>
public static class AttributeLogNames
{
    private static readonly Dictionary<string, string> Map = new()
    {
        [Key.Damage] = "伤害",
        [Key.Shield] = "护盾",
        [Key.Heal] = "治疗",
        [Key.Burn] = "灼烧",
        [Key.Poison] = "剧毒",
        [Key.CritRatePercent] = "暴击率",
        [Key.InFlight] = "飞行",
        [Key.FreezeRemainingMs] = "冻结",
        [Key.CooldownMs] = "冷却时间",
    };

    /// <summary>获取属性 key 的中文名；未收录时返回「属性」。</summary>
    public static string Get(string attributeName) => Map.TryGetValue(attributeName, out var name) ? name : "属性";
}

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
        ctx.ReportTriggerCause(Trigger.Burn);
    };

    /// <summary>剧毒：数值来自模板的 Poison。</summary>
    public static readonly Action<IEffectApplyContext> PoisonApply = ctx =>
    {
        int value = ctx.GetResolvedValue(Key.Poison, applyCritMultiplier: true);
        ctx.AddPoisonToOpp(value);
        ctx.LogEffect("剧毒", value, showCrit: ctx.IsCrit);
        ctx.ReportTriggerCause(Trigger.Poison);
    };

    /// <summary>对己方造成剧毒（如舱底蠕虫 S11「对自己造成剧毒」）；数值来自模板的 Poison。</summary>
    public static readonly Action<IEffectApplyContext> PoisonSelfApply = ctx =>
    {
        int value = ctx.GetResolvedValue(Key.Poison, applyCritMultiplier: true);
        ctx.AddPoisonToCaster(value);
        ctx.LogEffect("剧毒", value, showCrit: ctx.IsCrit);
        ctx.ReportTriggerCause(Trigger.Poison);
    };

    /// <summary>护盾：数值来自模板的 Shield。</summary>
    public static readonly Action<IEffectApplyContext> ShieldApply = ctx =>
    {
        int value = ctx.GetResolvedValue(Key.Shield, applyCritMultiplier: true);
        ctx.AddShieldToCaster(value);
        ctx.LogEffect("护盾", value, showCrit: ctx.IsCrit);
        ctx.ReportTriggerCause(Trigger.Shield);
    };

    /// <summary>治疗：数值来自模板的 Heal；治疗时清除 5% 灼烧/剧毒。日志与统计使用预期（请求）治疗量；实际施加量受生命上限限制。</summary>
    public static readonly Action<IEffectApplyContext> HealApply = ctx =>
    {
        int requested = ctx.GetResolvedValue(Key.Heal, applyCritMultiplier: true);
        ctx.HealCasterWithDebuffClear(requested);
        ctx.LogEffect("治疗", requested, showCrit: ctx.IsCrit);
    };

    /// <summary>生命再生：数值来自模板的 Regen。</summary>
    public static readonly Action<IEffectApplyContext> RegenApply = ctx =>
    {
        int value = ctx.GetResolvedValue(KeyRegen, applyCritMultiplier: true);
        ctx.AddRegenToCaster(value);
        ctx.LogEffect("生命再生", value, showCrit: ctx.IsCrit);
    };

    /// <summary>充能：根据 TargetCountKey（默认 ChargeTargetCount）与 Charge（毫秒）选取满足能力 TargetCondition 的己方物品施加充能（默认未摧毁且有冷却）；不参与暴击。</summary>
    public static readonly Action<IEffectApplyContext> ChargeApply = ctx =>
    {
        int chargeMs = ctx.GetResolvedValue(Key.Charge);
        string countKey = ctx.TargetCountKey ?? Key.ChargeTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyCharge(chargeMs, count, ctx.TargetCondition);
    };

    /// <summary>冻结：根据 TargetCountKey（默认 FreezeTargetCount）与 Freeze（毫秒）选取满足能力 TargetCondition 的敌人物品施加冻结（默认未摧毁且有冷却）；触发次数按实际目标数。</summary>
    public static readonly Action<IEffectApplyContext> FreezeApply = ctx =>
    {
        int freezeMs = ctx.GetResolvedValue(Key.Freeze);
        string countKey = ctx.TargetCountKey ?? Key.FreezeTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyFreeze(freezeMs, count, ctx.TargetCondition);
    };

    /// <summary>减速：根据 TargetCountKey（默认 SlowTargetCount）与 Slow（毫秒）选取满足能力 TargetCondition 的敌人物品施加减速（默认未摧毁且有冷却）。</summary>
    public static readonly Action<IEffectApplyContext> SlowApply = ctx =>
    {
        int slowMs = ctx.GetResolvedValue(Key.Slow);
        string countKey = ctx.TargetCountKey ?? Key.SlowTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplySlow(slowMs, count, ctx.TargetCondition);
    };

    /// <summary>对己方满足能力 TargetCondition 的物品增加指定属性（限本场战斗）。attributeName 为要增加的属性名（如 Damage、Poison、Key.InFlight）；目标条件由能力 TargetCondition 注入，默认 SameSide；实现层隐性要求目标未摧毁（与 Freeze 一致）。TargetCountKey 默认 ModifyAttributeTargetCount，默认值 20 表示全部，小于 20 时仅对至多该数量目标生效。</summary>
    public static Action<IEffectApplyContext> AddAttributeApply(string attributeName) => ctx =>
    {
        var targetCond = ctx.TargetCondition ?? Condition.SameSide;
        string countKey = ctx.TargetCountKey ?? Key.ModifyAttributeTargetCount;
        int cap = ctx.GetResolvedValue(countKey, defaultValue: 20);
        int maxTarget = cap >= 20 ? 0 : cap;
        ctx.AddAttributeToCasterSide(attributeName, ctx.Value, targetCond, maxTarget);
    };

    /// <summary>对满足能力 TargetCondition 的目标（从双方选取）减少指定属性（限本场战斗，不低于 0）。实现层隐性要求目标未摧毁（与 Freeze 一致）。目标数取自 TargetCountKey（默认 ModifyAttributeTargetCount）；日志名优先用 ctx.EffectLogName，否则属性中文名+「降低」。</summary>
    public static Action<IEffectApplyContext> ReduceAttributeApply(string attributeName) => ctx =>
    {
        var targetCond = ctx.TargetCondition ?? Condition.DifferentSide;
        string countKey = ctx.TargetCountKey ?? Key.ModifyAttributeTargetCount;
        int cap = ctx.GetResolvedValue(countKey, defaultValue: 20);
        int maxTarget = cap >= 20 ? 0 : cap;
        ctx.ReduceAttributeToSide(attributeName, ctx.Value, targetCond, maxTarget, ctx.EffectLogName);
    };

    /// <summary>加速：根据 TargetCountKey（默认 HasteTargetCount）与 Haste（毫秒）选取己方有冷却且满足能力 TargetCondition 的物品施加加速；未设 TargetCondition 时默认 SameSide。</summary>
    public static readonly Action<IEffectApplyContext> HasteApply = ctx =>
    {
        int hasteMs = ctx.GetResolvedValue(Key.Haste);
        string countKey = ctx.TargetCountKey ?? Key.HasteTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyHaste(hasteMs, count, ctx.TargetCondition);
    };

    /// <summary>修复：根据 TargetCountKey（默认 RepairTargetCount）与能力 TargetCondition 选取己方已摧毁物品进行修复（实现内会与 Condition.Destroyed 组合）；默认 SameSide。</summary>
    public static readonly Action<IEffectApplyContext> RepairApply = ctx =>
    {
        string countKey = ctx.TargetCountKey ?? Key.RepairTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyRepair(count, ctx.TargetCondition);
    };

    /// <summary>结束飞行：对己方满足 TargetCondition 且处于飞行状态的物品设为未飞行。目标默认己方且 InFlight（见 Ability.StopFlying）。</summary>
    public static readonly Action<IEffectApplyContext> StopFlyingApply = ctx =>
    {
        ctx.SetAttributeOnCasterSide(Key.InFlight, 0, ctx.TargetCondition);
    };

    /// <summary>摧毁：根据 TargetCountKey（默认 DestroyTargetCount）与能力 TargetCondition 选取己方未摧毁物品施加摧毁（默认 1 个）；先触发「摧毁物品时」，再标记 Destroyed。目标不要求有冷却。</summary>
    public static readonly Action<IEffectApplyContext> DestroyApply = ctx =>
    {
        string countKey = ctx.TargetCountKey ?? Key.DestroyTargetCount;
        int count = ctx.GetResolvedValue(countKey, defaultValue: 1);
        ctx.ApplyDestroy(count, ctx.TargetCondition);
    };
}
