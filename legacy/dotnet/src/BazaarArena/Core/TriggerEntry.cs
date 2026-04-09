namespace BazaarArena.Core;

public sealed class TriggerEntry
{
    public int Trigger { get; set; }
    public Formula Condition { get; set; } = Core.Condition.Always;
}
