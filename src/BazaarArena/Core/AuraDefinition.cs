namespace BazaarArena.Core;

/// <summary>单条光环定义：作用的属性、条件、以及固定/百分比数值来源字段。</summary>
public class AuraDefinition
{
    /// <summary>作用的属性名，如 "CritRatePercent"。</summary>
    public string AttributeName { get; set; } = "";

    /// <summary>光环条件：仅当条件满足时对该目标生效。使用 Condition.SameAsSource、Condition.AdjacentToSource 或 Condition.WithTag(tag)。</summary>
    public Condition? Condition { get; set; }

    /// <summary>提供固定加成的模板字段名（如 "Custom_0"）；无则 null。</summary>
    public string? FixedValueKey { get; set; }

    /// <summary>提供百分比加成的模板字段名（数值表示 +N%，加算）；无则 null。</summary>
    public string? PercentValueKey { get; set; }
}
