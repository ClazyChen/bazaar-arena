namespace BazaarArena.Core;

/// <summary>单条光环定义：作用的属性、条件、以及固定/百分比数值来源字段。</summary>
public class AuraDefinition
{
    /// <summary>作用的属性名，如 "CritRatePercent"。</summary>
    public string AttributeName { get; set; } = "";

    /// <summary>光环条件：仅当条件满足时对该目标生效。默认 SameAsSource；可使用 AdjacentToSource、WithTag(tag) 等。</summary>
    public Condition? Condition { get; set; } = Condition.SameAsSource;

    /// <summary>光环提供者需满足的条件；评估时 Item=Source=提供者，故可用 WithTag(tag)、InFlight 等表达「本物品在飞行」等。</summary>
    public Condition? SourceCondition { get; set; }

    /// <summary>提供固定加成的模板字段名（如 "Custom_0"）；无则 null。</summary>
    public string? FixedValueKey { get; set; }

    /// <summary>提供百分比加成的模板字段名（数值表示 +N%，加算）；无则 null。</summary>
    public string? PercentValueKey { get; set; }

    /// <summary>固定加成公式名（如 "SmallCountStash"）；若设置则忽略 FixedValueKey，由 BattleAuraContext 按公式计算。</summary>
    public string? FixedValueFormula { get; set; }
}
