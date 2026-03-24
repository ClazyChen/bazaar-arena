using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

public sealed class BattleState
{
    public BattleSide[] Side { get; } = new BattleSide[2];
    public BattleSessionTables? SessionTables { get; set; }
    public int TimeMs { get; set; }
    public IBattleLogSink LogSink { get; set; } = null!;
    public List<ItemState> CastQueue { get; } = [];
    public Action<int, ItemState?, ItemState?, int?>? TriggerInvoker { get; set; }
    internal AbilityQueueBuckets CurrentAbilityBuckets { get; } = new();
    internal AbilityQueueBuckets NextAbilityBuckets { get; } = new();
}
