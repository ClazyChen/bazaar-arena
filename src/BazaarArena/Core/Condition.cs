namespace BazaarArena.Core;

/// <summary>通用条件：用于光环或能力触发时的谓词判断（如目标是否与来源相同、被使用物品是否带某标签等）。</summary>
public class Condition
{
    /// <summary>条件种类。</summary>
    public ConditionKind Kind { get; set; }

    /// <summary>当 Kind 为 WithTag 时，要检查的标签。</summary>
    public string? Tag { get; set; }

    /// <summary>目标与来源相同（光环：targetItemIndex == sourceItemIndex；触发器：仅来源物品触发）。无参，不带括号使用。</summary>
    public static Condition SameAsSource { get; } = new() { Kind = ConditionKind.SameAsSource };

    /// <summary>目标与来源不同（触发器：除来源物品外的物品触发，如 UseOtherItem）。无参，不带括号使用。</summary>
    public static Condition DifferentFromSource { get; } = new() { Kind = ConditionKind.DifferentFromSource };

    /// <summary>目标与来源相邻（光环：|sourceIndex - targetIndex| == 1）。无参，不带括号使用。</summary>
    public static Condition AdjacentToSource { get; } = new() { Kind = ConditionKind.AdjacentToSource };

    /// <summary>有参条件：被使用物品带指定标签时成立。使用方式：Condition.WithTag(Tag.Tool)。</summary>
    public static Condition WithTag(string tag) => new() { Kind = ConditionKind.WithTag, Tag = tag };
}

/// <summary>条件种类。</summary>
public enum ConditionKind
{
    /// <summary>目标与来源相同。</summary>
    SameAsSource,

    /// <summary>目标与来源不同（如 UseOtherItem 时排除施放物品自身）。</summary>
    DifferentFromSource,

    /// <summary>目标与来源相邻。</summary>
    AdjacentToSource,

    /// <summary>带指定标签（如用于「被使用的物品」带 Tool 标签）。</summary>
    WithTag,
}
