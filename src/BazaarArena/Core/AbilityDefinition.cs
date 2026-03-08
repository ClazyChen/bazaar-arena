namespace BazaarArena.Core;

/// <summary>能力定义：触发器名、优先级与效果列表。触发间隔 5 帧（250ms）由模拟器维护。</summary>
public class AbilityDefinition
{
    /// <summary>触发器名字，如「使用物品」。</summary>
    public string TriggerName { get; set; } = "";

    public AbilityPriority Priority { get; set; }

    /// <summary>该能力触发的效果列表（伤害、灼烧等）。</summary>
    public List<EffectDefinition> Effects { get; set; } = new();
}

/// <summary>单条效果定义：类型与数值（基座阶段可为固定值，后续可支持按等级）。</summary>
public class EffectDefinition
{
    public EffectKind Kind { get; set; }
    public int Value { get; set; }
}
