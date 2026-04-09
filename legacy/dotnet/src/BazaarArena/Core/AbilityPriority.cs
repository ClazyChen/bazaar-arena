namespace BazaarArena.Core;

/// <summary>能力优先级，从高到低。Immediate 同帧执行，其余下一帧。</summary>
public enum AbilityPriority
{
    Immediate,
    Highest,
    High,
    Medium,
    Low,
    Lowest,
}
