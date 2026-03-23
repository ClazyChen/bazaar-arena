namespace BazaarArena.BattleSimulator;

/// <summary>能力队列项：待执行的能力效果（含多重释放剩余次数）。250ms 触发间隔由能力持有者物品上的 LastTriggerMs 状态统一保证。</summary>
public class AbilityQueueEntry
{
    /// <summary>能力持有者（引用）。</summary>
    public ItemState Owner { get; set; } = null!;
    public int AbilityIndex { get; set; }
    public int PendingCount { get; set; }
    /// <summary>当非 null 时表示本条目由触发器指向的单个目标触发，效果应对该物品施加（如月光宝珠「敌方加速时令其减速」）；不与其他条目合并。</summary>
    public int? InvokeTargetSideIndex { get; set; }
    public int? InvokeTargetItemIndex { get; set; }
}
