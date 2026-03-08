namespace BazaarArena.Core;

/// <summary>等级提升条目：来自 levelups.json 的单个等级生命值增量。</summary>
public class LevelUpEntry
{
    public int Level { get; set; }
    public int HealthIncrease { get; set; }
}
