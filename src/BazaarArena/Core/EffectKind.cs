namespace BazaarArena.Core;

/// <summary>能力效果类型：伤害、灼烧、剧毒、护盾、治疗、生命再生等。</summary>
public enum EffectKind
{
    Damage,
    Burn,
    Poison,
    Shield,
    Heal,
    Regen,
    /// <summary>自定义效果，由 CustomEffectId 指定逻辑。</summary>
    Other,
}
