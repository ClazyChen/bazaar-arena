namespace BazaarArena.Core;

/// <summary>单条光环定义：作用的属性、条件、以及数值公式。Value 为公式（如 Formula.Source(key)）；Percent 为 true 时表示百分比加成（+N% 加算），默认 false 为固定值加成。</summary>
public class AuraDefinition
{
    /// <summary>作用的属性名，如 "CritRatePercent"。</summary>
    public string AttributeName { get; set; } = "";

    /// <summary>光环条件：仅当条件满足时对该目标生效。默认 SameAsSource；可使用 AdjacentToSource、WithTag(tag) 等。</summary>
    public Condition? Condition { get; set; } = Condition.SameAsSource;

    /// <summary>光环提供者需满足的条件；评估时 Item=Source=提供者，故可用 WithTag(tag)、InFlight 等表达「本物品在飞行」等。</summary>
    public Condition? SourceCondition { get; set; }

    /// <summary>数值公式，由 Formula.Evaluate 计算。如 Formula.Source(Key.Custom_0)、Formula.Opp(BattleSide.KeyPoison) 等；null 表示不提供数值。</summary>
    public Formula? Value { get; set; }

    /// <summary>为 true 时公式结果为百分比加成（+N% 加算），为 false 时为固定值加成。默认 false。</summary>
    public bool Percent { get; set; }

    /// <summary>授予的标签列表（如 [Tag.Vehicle]）；非空时表示「满足 Condition 的目标在条件评估时视为带这些标签」。仅用于「相邻视为载具」等光环，与 AttributeName/Value 互斥。</summary>
    public IReadOnlyList<string>? GrantedTags { get; set; }
}
