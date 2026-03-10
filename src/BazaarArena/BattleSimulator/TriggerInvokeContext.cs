using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>触发器调用上下文：传入 InvokeTrigger，用于条件判断与 PendingCount。</summary>
internal sealed class TriggerInvokeContext
{
    public int? Multicast { get; init; }
    public ItemTemplate? UsedTemplate { get; init; }
    /// <summary>OnDestroy 时：被摧毁物品的模板（用于条件如「被毁目标为大型或飞行」）。</summary>
    public ItemTemplate? DestroyedItemTemplate { get; init; }
    /// <summary>OnDestroy 时：被摧毁物品是否处于飞行状态。</summary>
    public bool DestroyedItemInFlight { get; init; }
}
