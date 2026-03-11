namespace BazaarArena.BattleSimulator;

/// <summary>能力队列项：待执行的能力效果（含多重触发剩余次数）。</summary>
public class AbilityQueueEntry
{
    /// <summary>能力持有者（引用）。</summary>
    public BattleItemState Owner { get; set; } = null!;
    public int AbilityIndex { get; set; }
    public int PendingCount { get; set; }
    public int LastTriggerMs { get; set; }
}
