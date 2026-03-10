namespace BazaarArena.Core;

/// <summary>能力触发类型常量，用于 AbilityDefinition.TriggerName，避免魔法字符串。</summary>
public static class Trigger
{
    public const string UseItem = "使用物品";
    public const string BattleStart = "战斗开始";
    /// <summary>使用其他物品时触发（如姜饼人：使用工具时为此物品充能）。</summary>
    public const string UseOtherItem = "使用其他物品";
    /// <summary>己方施加冻结时触发（每冻结一个目标计一次，由 PendingCount 控制 250ms 间隔）。</summary>
    public const string Freeze = "触发冻结";
}
