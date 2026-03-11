namespace BazaarArena.Core;

/// <summary>效果数值结算委托：根据物品模板与等级返回效果数值（如伤害、灼烧量等）。</summary>
public delegate int EffectValueResolver(ItemTemplate template, ItemTier tier);

/// <summary>能力定义：触发器名、优先级与效果列表。触发间隔 5 帧（250ms）由模拟器维护。</summary>
public class AbilityDefinition
{
    /// <summary>触发器名字，如「使用物品」。</summary>
    public string TriggerName { get; set; } = "";

    /// <summary>能力优先级；默认 Medium，仅非默认时需在定义中显式指定。</summary>
    public AbilityPriority Priority { get; set; } = AbilityPriority.Medium;

    /// <summary>引起触发器的物品（source，如「被使用的物品」）需满足的条件。可选，null 时由触发器决定默认（UseItem → SameAsSource，其他 → SameSide）。</summary>
    public Condition? Condition { get; set; }

    /// <summary>触发器所指向的物品需满足的条件（如 Slow 时被减速的物品、Freeze 时被冻结的物品）。默认 null 表示不限制。</summary>
    public Condition? InvokeTargetCondition { get; set; }

    /// <summary>多目标效果（冻结/减速/充能/加速）的目标选择条件；如不设则由效果默认（DifferentSide 或 SameSide）。</summary>
    public Condition? TargetCondition { get; set; }

    /// <summary>该能力触发的效果列表（伤害、灼烧等）。</summary>
    public List<EffectDefinition> Effects { get; set; } = [];
}

/// <summary>单条效果定义：数值结算（ValueKey/ValueResolver）与应用委托 Apply。暴击是否乘到数值上由模拟器根据物品六字段与 ApplyCritMultiplier 决定。</summary>
public class EffectDefinition
{
    /// <summary>固定数值；当无 ValueResolver/ValueKey 且模板对应字段为 0 时使用。</summary>
    public int Value { get; set; }
    /// <summary>结算委托：返回本效果数值；设置后优先使用此委托。</summary>
    public EffectValueResolver? ValueResolver { get; set; }
    /// <summary>模板字段名，如 "Damage"、"Custom_0"；当未设置 ValueResolver 时，用 template.GetInt(ValueKey, tier) 结算数值。</summary>
    public string? ValueKey { get; set; }
    /// <summary>是否对基础数值乘暴击倍率；Charge/Freeze/Slow 等为 false。</summary>
    public bool ApplyCritMultiplier { get; set; } = true;
    /// <summary>效果应用委托；由 Core/Effect 预定义或自定义效果设置。</summary>
    public Action<IEffectApplyContext>? Apply { get; set; }

    /// <summary>按优先级解析数值：ValueResolver → ValueKey → defaultKey；结果为 0 时用 Value。</summary>
    public int ResolveValue(ItemTemplate template, ItemTier tier, string defaultKey)
    {
        string key = !string.IsNullOrEmpty(ValueKey) ? ValueKey : defaultKey;
        int v = ValueResolver != null ? ValueResolver(template, tier)
            : template.GetInt(key, tier);
        return v != 0 ? v : Value;
    }
}
