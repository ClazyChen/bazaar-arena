namespace BazaarArena.Core;

/// <summary>能力触发类型常量，用于 AbilityDefinition.TriggerName，避免魔法字符串。</summary>
public static class Trigger
{
    public const string UseItem = "使用物品";
    public const string BattleStart = "战斗开始";
    /// <summary>己方施加冻结时触发（每冻结一个目标计一次，由 PendingCount 控制 250ms 间隔）。</summary>
    public const string Freeze = "触发冻结";
    /// <summary>己方施加减速时触发（每减速一个目标计一次，由 PendingCount 控制 250ms 间隔）。</summary>
    public const string Slow = "触发减速";
    /// <summary>己方造成暴击时触发（来源=暴击施放者，候选=己方所有带此能力的物品）。</summary>
    public const string OnCrit = "造成暴击时";
    /// <summary>己方某物品被摧毁时触发（来源=造成本次摧毁的物品，候选=持有该能力的物品）；须在将目标标记为 Destroyed 之前调用。</summary>
    public const string OnDestroy = "摧毁物品时";
}
