namespace BazaarArena.Core;

public class AuraDefinition
{
    public int Attribute { get; set; }
    public Formula Condition { get; set; } = Core.Condition.SameAsCaster;
    public Formula? Value { get; set; }
    public bool Percent { get; set; }
}
