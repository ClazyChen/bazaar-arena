using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>单个运行时能力引用在单场战斗中的状态。</summary>
public sealed class AbilityState
{
    public int LastTriggerMs { get; set; } = int.MinValue;
    public int PendingCount { get; set; }
    public List<ItemState>? InvokeTargets { get; set; }
}

