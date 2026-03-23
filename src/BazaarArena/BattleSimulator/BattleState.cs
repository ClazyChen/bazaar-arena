namespace BazaarArena.BattleSimulator;

public sealed class BattleState
{
    public BattleSide Side0 { get; set; } = null!;
    public BattleSide Side1 { get; set; } = null!;
    public int TimeMs { get; set; }
    public List<ItemState> CastQueue { get; } = [];
    internal AbilityQueueBuckets CurrentAbilityBuckets { get; } = new();
    internal AbilityQueueBuckets NextAbilityBuckets { get; } = new();
}
