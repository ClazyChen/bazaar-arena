using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>触发器调用上下文：传入 InvokeTrigger，用于条件判断与 PendingCount。</summary>
internal sealed class TriggerInvokeContext
{
    public int? Multicast { get; init; }
    public ItemTemplate? UsedTemplate { get; init; }
    /// <summary>触发器所指向的目标物品（如 Slow/Freeze/Destroy 时被减速/被冻结/被摧毁的物品）。用于 InvokeTargetCondition 筛选。</summary>
    public BattleItemState? InvokeTargetItem { get; init; }
}
