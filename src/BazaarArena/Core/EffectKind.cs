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
    /// <summary>充能：为此物品增加已过冷却时间（毫秒），不能暴击。</summary>
    Charge,
    /// <summary>冻结：随机选取敌人物品施加冻结时长（毫秒），有冷却的物品优先；不能暴击。</summary>
    Freeze,
    /// <summary>自定义效果，由 CustomEffectId 指定逻辑。</summary>
    Other,
}
