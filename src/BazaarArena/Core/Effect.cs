namespace BazaarArena.Core;

/// <summary>预定义效果：只读效果在委托内用 ctx.GetResolvedValue(key) 按常量 key 取值，不设 ValueKey；仅 WeaponDamageBonus 保留 ValueKey 以指定数值来源字段。</summary>
public static class Effect
{
    /// <summary>模板中生命再生字段的 key（ItemTemplate 无 Regen 属性，用常量避免魔法字符串）。</summary>
    private const string KeyRegen = "Regen";

    /// <summary>造成伤害：数值来自模板的 Damage；吸血时治疗己方并显示「吸血」。</summary>
    public static readonly EffectDefinition Damage = new()
    {
        Apply = ctx =>
        {
            int value = ctx.GetResolvedValue(nameof(ItemTemplate.Damage), applyCritMultiplier: true);
            int actualHp = ctx.ApplyDamageToOpp(value, isBurn: false);
            if (ctx.HasLifeSteal && actualHp > 0) ctx.HealCaster(actualHp);
            ctx.LogEffect(ctx.HasLifeSteal ? "吸血" : "伤害", value, showCrit: ctx.IsCrit);
        },
    };

    /// <summary>灼烧：数值来自模板的 Burn。</summary>
    public static readonly EffectDefinition Burn = new()
    {
        Apply = ctx =>
        {
            int value = ctx.GetResolvedValue(nameof(ItemTemplate.Burn), applyCritMultiplier: true);
            ctx.AddBurnToOpp(value);
            ctx.LogEffect("灼烧", value, showCrit: ctx.IsCrit);
        },
    };

    /// <summary>剧毒：数值来自模板的 Poison。</summary>
    public static readonly EffectDefinition Poison = new()
    {
        Apply = ctx =>
        {
            int value = ctx.GetResolvedValue(nameof(ItemTemplate.Poison), applyCritMultiplier: true);
            ctx.AddPoisonToOpp(value);
            ctx.LogEffect("剧毒", value, showCrit: ctx.IsCrit);
        },
    };

    /// <summary>护盾：数值来自模板的 Shield。</summary>
    public static readonly EffectDefinition Shield = new()
    {
        Apply = ctx =>
        {
            int value = ctx.GetResolvedValue(nameof(ItemTemplate.Shield), applyCritMultiplier: true);
            ctx.AddShieldToCaster(value);
            ctx.LogEffect("护盾", value, showCrit: ctx.IsCrit);
        },
    };

    /// <summary>治疗：数值来自模板的 Heal；治疗时清除 5% 灼烧/剧毒。</summary>
    public static readonly EffectDefinition Heal = new()
    {
        Apply = ctx =>
        {
            int value = ctx.GetResolvedValue(nameof(ItemTemplate.Heal), applyCritMultiplier: true);
            int actual = ctx.HealCasterWithDebuffClear(value);
            ctx.LogEffect("治疗", actual, showCrit: ctx.IsCrit);
        },
    };

    /// <summary>生命再生：数值来自模板的 Regen。</summary>
    public static readonly EffectDefinition Regen = new()
    {
        Apply = ctx =>
        {
            int value = ctx.GetResolvedValue(KeyRegen, applyCritMultiplier: true);
            ctx.AddRegenToCaster(value);
            ctx.LogEffect("生命再生", value, showCrit: ctx.IsCrit);
        },
    };

    /// <summary>为此物品充能（毫秒）；不参与暴击。</summary>
    public static readonly EffectDefinition ChargeSelf = new()
    {
        ApplyCritMultiplier = false,
        Apply = ctx =>
        {
            int value = ctx.GetResolvedValue(nameof(ItemTemplate.Charge), applyCritMultiplier: false);
            ctx.ChargeCasterItem(value, out _);
        },
    };

    /// <summary>冻结：根据 FreezeTargetCount 与 Freeze（毫秒）选取敌人物品施加冻结。</summary>
    public static readonly EffectDefinition Freeze = new()
    {
        ApplyCritMultiplier = false,
        Apply = ctx =>
        {
            int freezeMs = ctx.GetResolvedValue(nameof(ItemTemplate.Freeze), applyCritMultiplier: false);
            int count = ctx.GetCasterItemInt(nameof(ItemTemplate.FreezeTargetCount), 1);
            ctx.ApplyFreeze(freezeMs, count);
        },
    };

    /// <summary>减速：根据 SlowTargetCount 与 Slow（毫秒）选取敌人物品施加减速。</summary>
    public static readonly EffectDefinition Slow = new()
    {
        ApplyCritMultiplier = false,
        Apply = ctx =>
        {
            int slowMs = ctx.GetResolvedValue(nameof(ItemTemplate.Slow), applyCritMultiplier: false);
            int count = ctx.GetCasterItemInt(nameof(ItemTemplate.SlowTargetCount), 1);
            ctx.ApplySlow(slowMs, count);
        },
    };

    /// <summary>武器伤害提升：对己方所有武器物品 Damage 增加指定量；数值来自 ValueKey 字段（默认 Custom_0）。日志为「伤害提高 →[目标]」。</summary>
    public static EffectDefinition WeaponDamageBonus(string? ValueKey = null) => new()
    {
        ValueKey = ValueKey ?? nameof(ItemTemplate.Custom_0),
        ApplyCritMultiplier = false,
        Apply = ctx => ctx.AddWeaponDamageBonusToCasterSide(ctx.Value),
    };

    /// <summary>加速：对施放者右侧物品（ItemIndex+1）施加加速，时长来自模板 Haste（毫秒）或 HasteSeconds（秒）。</summary>
    public static readonly EffectDefinition Accelerate = new()
    {
        ApplyCritMultiplier = false,
        Apply = ctx =>
        {
            int hasteMs = ctx.GetResolvedValue(nameof(ItemTemplate.Haste), applyCritMultiplier: false);
            ctx.ApplyHaste(hasteMs, ctx.ItemIndex + 1);
        },
    };

    /// <summary>对施放者右侧物品（ItemIndex+1）若为武器则增加伤害；数值来自 ValueKey（默认 Custom_0）。日志为「伤害提高 →[目标]」。</summary>
    public static EffectDefinition WeaponDamageBonusToRightItem(string? ValueKey = null) => new()
    {
        ValueKey = ValueKey ?? nameof(ItemTemplate.Custom_0),
        ApplyCritMultiplier = false,
        Apply = ctx => ctx.AddWeaponDamageBonusToCasterSideItem(ctx.Value, ctx.ItemIndex + 1),
    };
}
