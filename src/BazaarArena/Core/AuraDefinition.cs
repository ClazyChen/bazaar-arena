namespace BazaarArena.Core;

/// <summary>光环条件种类：用于判断光环是否作用于「被计算属性的物品」。</summary>
public enum AuraConditionKind
{
    /// <summary>被计算属性的物品与光环来源物品在己方槽位中相邻（|sourceIndex - targetIndex| == 1）。</summary>
    AdjacentToSource,
}

/// <summary>单条光环定义：作用的属性、条件、以及固定/百分比数值来源字段。</summary>
public class AuraDefinition
{
    /// <summary>作用的属性名，如 "CritRatePercent"。</summary>
    public string AttributeName { get; set; } = "";

    /// <summary>光环条件：仅当条件满足时对该目标生效。</summary>
    public AuraConditionKind Condition { get; set; }

    /// <summary>提供固定加成的模板字段名（如 "Custom_0"）；无则 null。</summary>
    public string? FixedValueKey { get; set; }

    /// <summary>提供百分比加成的模板字段名（数值表示 +N%，加算）；无则 null。</summary>
    public string? PercentValueKey { get; set; }
}
