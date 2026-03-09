namespace BazaarArena.Core;

/// <summary>预定义效果：使用物品模板上对应字段（Damage、Burn 等）结算数值，可直接用于 Abilities 的 Effects 列表。</summary>
public static class Effect
{
    /// <summary>造成伤害：数值来自模板的 Damage 字段（可单值或按等级）。</summary>
    public static readonly EffectDefinition Damage = new()
    {
        Kind = EffectKind.Damage,
        ValueResolver = (t, tier) => t.GetInt("Damage", tier),
    };

    /// <summary>灼烧：数值来自模板的 Burn 字段。</summary>
    public static readonly EffectDefinition Burn = new()
    {
        Kind = EffectKind.Burn,
        ValueResolver = (t, tier) => t.GetInt("Burn", tier),
    };

    /// <summary>剧毒：数值来自模板的 Poison 字段。</summary>
    public static readonly EffectDefinition Poison = new()
    {
        Kind = EffectKind.Poison,
        ValueResolver = (t, tier) => t.GetInt("Poison", tier),
    };

    /// <summary>护盾：数值来自模板的 Shield 字段。</summary>
    public static readonly EffectDefinition Shield = new()
    {
        Kind = EffectKind.Shield,
        ValueResolver = (t, tier) => t.GetInt("Shield", tier),
    };

    /// <summary>治疗：数值来自模板的 Heal 字段。</summary>
    public static readonly EffectDefinition Heal = new()
    {
        Kind = EffectKind.Heal,
        ValueResolver = (t, tier) => t.GetInt("Heal", tier),
    };

    /// <summary>生命再生：数值来自模板的 Regen 字段。</summary>
    public static readonly EffectDefinition Regen = new()
    {
        Kind = EffectKind.Regen,
        ValueResolver = (t, tier) => t.GetInt("Regen", tier),
    };
}
