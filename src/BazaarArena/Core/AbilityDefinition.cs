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

/// <summary>单条效果定义：类型、可选固定数值，以及可选的结算委托（委托优先于模板字段与 Value）。</summary>
public class EffectDefinition
{
    public EffectKind Kind { get; set; }
    /// <summary>固定数值；当无 ValueResolver 且模板对应字段为 0 时使用。</summary>
    public int Value { get; set; }
    /// <summary>结算委托：返回本效果数值；设置后模拟器优先使用此委托而非模板字段。</summary>
    public EffectValueResolver? ValueResolver { get; set; }
}
