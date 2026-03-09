namespace BazaarArena.Core;

/// <summary>效果数值结算委托：根据物品模板与等级返回效果数值（如伤害、灼烧量等）。</summary>
public delegate int EffectValueResolver(ItemTemplate template, ItemTier tier);

/// <summary>能力定义：触发器名、优先级与效果列表。触发间隔 5 帧（250ms）由模拟器维护。</summary>
public class AbilityDefinition
{
    /// <summary>触发器名字，如「使用物品」。</summary>
    public string TriggerName { get; set; } = "";

    public AbilityPriority Priority { get; set; }

    /// <summary>该能力触发的效果列表（伤害、灼烧等）。</summary>
    public List<EffectDefinition> Effects { get; set; } = [];
}

/// <summary>单条效果定义：类型、可选固定数值，以及可选的结算方式（ValueResolver 优先，否则按 ValueKey 读模板字段）。</summary>
public class EffectDefinition
{
    public EffectKind Kind { get; set; }
    /// <summary>固定数值；当无 ValueResolver/ValueKey 且模板对应字段为 0 时使用。</summary>
    public int Value { get; set; }
    /// <summary>结算委托：返回本效果数值；设置后优先使用此委托。</summary>
    public EffectValueResolver? ValueResolver { get; set; }
    /// <summary>模板字段名，如 "Custom_0"；当未设置 ValueResolver 时，用 template.GetInt(ValueKey, tier) 结算数值。</summary>
    public string? ValueKey { get; set; }
    /// <summary>自定义效果 ID（仅当 Kind 为 Other 时使用），由模拟器按 ID 派发到具体处理逻辑。</summary>
    public string? CustomEffectId { get; set; }

    /// <summary>按优先级解析数值：ValueResolver → ValueKey → defaultKey；结果为 0 时用 Value。</summary>
    public int ResolveValue(ItemTemplate template, ItemTier tier, string defaultKey)
    {
        int v = ValueResolver != null ? ValueResolver(template, tier)
            : !string.IsNullOrEmpty(ValueKey) ? template.GetInt(ValueKey, tier)
            : template.GetInt(defaultKey, tier);
        return v != 0 ? v : Value;
    }
}
