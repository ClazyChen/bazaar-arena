using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>触发器调用上下文：传入 InvokeTrigger，用于条件判断与 PendingCount。</summary>
internal sealed class TriggerInvokeContext
{
    public int? Multicast { get; init; }
    public ItemTemplate? UsedTemplate { get; init; }
}
