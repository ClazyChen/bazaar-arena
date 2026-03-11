namespace BazaarArena.Core;

/// <summary>能力定义：触发器名、优先级与效果应用（Apply）。触发间隔 5 帧（250ms）由模拟器维护。</summary>
public class AbilityDefinition
{
    /// <summary>触发器名字，如「使用物品」。</summary>
    public string TriggerName { get; set; } = "";

    /// <summary>能力优先级；默认 Medium，仅非默认时需在定义中显式指定。</summary>
    public AbilityPriority Priority { get; set; } = AbilityPriority.Medium;

    /// <summary>引起触发的物品（ConditionContext.Item，如「被使用的物品」）需满足的条件。可选，null 时由触发器决定默认（UseItem → SameAsSource，其他 → SameSide）。评估时 Source=能力持有者、Item=引起触发的物品。</summary>
    public Condition? Condition { get; set; }

    /// <summary>能力持有者需满足的条件；评估时 Item=Source=能力持有者，故可用 WithTag(tag)、InFlight 等表达「本物品带某 tag / 在飞行」等。</summary>
    public Condition? SourceCondition { get; set; }

    /// <summary>触发器所指向的物品（ConditionContext.Item）需满足的条件（如 Slow 时被减速的物品）。默认 null 表示不限制。评估时 Source=能力持有者、Item=指向的物品。</summary>
    public Condition? InvokeTargetCondition { get; set; }

    /// <summary>多目标效果（冻结/减速/充能/加速）的目标选择条件；评估时 Source=能力持有者、Item=候选目标。如不设则由效果默认（DifferentSide 或 SameSide）。</summary>
    public Condition? TargetCondition { get; set; }

    /// <summary>固定数值；当 ValueKey 对应模板字段为 0 时使用。</summary>
    public int Value { get; set; }

    /// <summary>模板字段名，如 "Damage"、"Custom_0"；用 template.GetInt(ValueKey, tier) 结算数值，未设时由 Apply 委托内用 GetResolvedValue(key) 取值。</summary>
    public string? ValueKey { get; set; }

    /// <summary>是否对基础数值乘暴击倍率；Charge/Freeze/Slow 等为 false。</summary>
    public bool ApplyCritMultiplier { get; set; } = true;

    /// <summary>效果应用委托；由 Core/Effect 预定义或自定义效果设置。</summary>
    public Action<IEffectApplyContext>? Apply { get; set; }

    /// <summary>解析数值：ValueKey 或 defaultKey 对应 template.GetInt；结果为 0 时用 Value。</summary>
    public int ResolveValue(ItemTemplate template, ItemTier tier, string defaultKey)
    {
        string key = !string.IsNullOrEmpty(ValueKey) ? ValueKey : defaultKey;
        int v = template.GetInt(key, tier);
        return v != 0 ? v : Value;
    }
}
