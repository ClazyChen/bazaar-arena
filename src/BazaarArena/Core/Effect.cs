namespace BazaarArena.Core;

/// <summary>预定义效果应用委托：只读效果在委托内用 ctx.GetResolvedValue(key) 按常量 key 取值，不设 ValueKey；指定了 ValueKey 的能力（如 AddAttribute、ReduceAttribute）由模拟器填入 ctx.Value。</summary>
public static class Effect
{
    /// <summary>模板中生命再生字段的 key（ItemTemplate 无 Regen 属性，用常量避免魔法字符串）。</summary>
    private const string KeyRegen = "Regen";

    /// <summary>造成伤害：数值来自模板的 Damage；吸血时治疗己方并显示「吸血」。</summary>
    public static readonly Action<IEffectApplyContext> DamageApply = ctx =>
    {
        int value = ctx.GetResolvedValue(nameof(ItemTemplate.Damage), applyCritMultiplier: true);
        int actualHp = ctx.ApplyDamageToOpp(value, isBurn: false);
        if (ctx.HasLifeSteal && actualHp > 0) ctx.HealCaster(actualHp);
        ctx.LogEffect(ctx.HasLifeSteal ? "吸血" : "伤害", value, showCrit: ctx.IsCrit);
    };

    /// <summary>灼烧：数值来自模板的 Burn。</summary>
    public static readonly Action<IEffectApplyContext> BurnApply = ctx =>
    {
        int value = ctx.GetResolvedValue(nameof(ItemTemplate.Burn), applyCritMultiplier: true);
        ctx.AddBurnToOpp(value);
        ctx.LogEffect("灼烧", value, showCrit: ctx.IsCrit);
    };

    /// <summary>剧毒：数值来自模板的 Poison。</summary>
    public static readonly Action<IEffectApplyContext> PoisonApply = ctx =>
    {
        int value = ctx.GetResolvedValue(nameof(ItemTemplate.Poison), applyCritMultiplier: true);
        ctx.AddPoisonToOpp(value);
        ctx.LogEffect("剧毒", value, showCrit: ctx.IsCrit);
    };

    /// <summary>护盾：数值来自模板的 Shield。</summary>
    public static readonly Action<IEffectApplyContext> ShieldApply = ctx =>
    {
        int value = ctx.GetResolvedValue(nameof(ItemTemplate.Shield), applyCritMultiplier: true);
        ctx.AddShieldToCaster(value);
        ctx.LogEffect("护盾", value, showCrit: ctx.IsCrit);
    };

    /// <summary>治疗：数值来自模板的 Heal；治疗时清除 5% 灼烧/剧毒。</summary>
    public static readonly Action<IEffectApplyContext> HealApply = ctx =>
    {
        int value = ctx.GetResolvedValue(nameof(ItemTemplate.Heal), applyCritMultiplier: true);
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

    /// <summary>为此物品充能（毫秒）；不参与暴击。</summary>
    public static readonly Action<IEffectApplyContext> ChargeSelfApply = ctx =>
    {
        int value = ctx.GetResolvedValue(nameof(ItemTemplate.Charge));
        ctx.ChargeCasterItem(value, out _);
    };

    /// <summary>冻结：根据 FreezeTargetCount 与 Freeze（毫秒）选取有冷却的敌人物品施加冻结（默认 Condition.DifferentSide），触发次数按实际目标数。</summary>
    public static readonly Action<IEffectApplyContext> FreezeApply = ctx =>
    {
        int freezeMs = ctx.GetResolvedValue(nameof(ItemTemplate.Freeze));
        int count = ctx.GetResolvedValue(nameof(ItemTemplate.FreezeTargetCount), defaultValue: 1);
        ctx.ApplyFreeze(freezeMs, count, null);
    };

    /// <summary>减速：根据 SlowTargetCount 与 Slow（毫秒）选取有冷却的敌人物品施加减速（默认 Condition.DifferentSide）。</summary>
    public static readonly Action<IEffectApplyContext> SlowApply = ctx =>
    {
        int slowMs = ctx.GetResolvedValue(nameof(ItemTemplate.Slow));
        int count = ctx.GetResolvedValue(nameof(ItemTemplate.SlowTargetCount), defaultValue: 1);
        ctx.ApplySlow(slowMs, count, null);
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
        int hasteMs = ctx.GetResolvedValue(nameof(ItemTemplate.Haste));
        int count = ctx.GetResolvedValue(nameof(ItemTemplate.HasteTargetCount), defaultValue: 1);
        ctx.ApplyHaste(hasteMs, count, ctx.TargetCondition);
    };

    /// <summary>修复：根据 RepairTargetCount 与能力 TargetCondition 选取己方已摧毁物品进行修复（未摧毁、冷却重置）；默认 SameSide。</summary>
    public static readonly Action<IEffectApplyContext> RepairApply = ctx =>
    {
        int count = ctx.GetResolvedValue(nameof(ItemTemplate.RepairTargetCount), defaultValue: 1);
        ctx.ApplyRepair(count, ctx.TargetCondition);
    };

    /// <summary>施放者物品开始飞行（设置运行时飞行状态）；若已在飞行则不重复结算、不记日志。</summary>
    public static readonly Action<IEffectApplyContext> StartFlyingApply = ctx =>
    {
        if (ctx.IsCasterInFlight) return;
        ctx.SetCasterInFlight(true);
        ctx.LogEffect("开始飞行", 0, showCrit: false);
    };

    /// <summary>摧毁己方施放者右侧下一件未摧毁物品（牵引光束等）；先触发「摧毁物品时」，再标记 Destroyed。</summary>
    public static readonly Action<IEffectApplyContext> DestroyNextItemToRightOfCasterApply = ctx =>
        ctx.DestroyNextItemToRightOfCaster();
}
