namespace BazaarArena.Core;

/// <summary>预定义效果：使用物品模板上对应字段（Damage、Burn 等）结算数值，可直接用于 Abilities 的 Effects 列表。</summary>
public static class Effect
{
    /// <summary>造成伤害：数值来自模板的 Damage 字段（可单值或按等级）。</summary>
    public static readonly EffectDefinition Damage = new()
    {
        Kind = EffectKind.Damage,
        ValueKey = EffectKind.Damage.GetDefaultTemplateKey(),
    };

    /// <summary>灼烧：数值来自模板的 Burn 字段。</summary>
    public static readonly EffectDefinition Burn = new()
    {
        Kind = EffectKind.Burn,
        ValueKey = EffectKind.Burn.GetDefaultTemplateKey(),
    };

    /// <summary>剧毒：数值来自模板的 Poison 字段。</summary>
    public static readonly EffectDefinition Poison = new()
    {
        Kind = EffectKind.Poison,
        ValueKey = EffectKind.Poison.GetDefaultTemplateKey(),
    };

    /// <summary>护盾：数值来自模板的 Shield 字段。</summary>
    public static readonly EffectDefinition Shield = new()
    {
        Kind = EffectKind.Shield,
        ValueKey = EffectKind.Shield.GetDefaultTemplateKey(),
    };

    /// <summary>治疗：数值来自模板的 Heal 字段。</summary>
    public static readonly EffectDefinition Heal = new()
    {
        Kind = EffectKind.Heal,
        ValueKey = EffectKind.Heal.GetDefaultTemplateKey(),
    };

    /// <summary>生命再生：数值来自模板的 Regen 字段。</summary>
    public static readonly EffectDefinition Regen = new()
    {
        Kind = EffectKind.Regen,
        ValueKey = EffectKind.Regen.GetDefaultTemplateKey(),
    };

    /// <summary>武器伤害提升（自定义效果）：对己方所有带「武器」tag 的物品，将其 Damage 增加指定量；数值来自模板的 valueKey 字段（默认 Custom_0）。</summary>
    public static EffectDefinition WeaponDamageBonus(string? ValueKey = null) => new()
    {
        Kind = EffectKind.Other,
        CustomEffectId = "WeaponDamageBonus",
        ValueKey = ValueKey ?? nameof(ItemTemplate.Custom_0),
    };
}
