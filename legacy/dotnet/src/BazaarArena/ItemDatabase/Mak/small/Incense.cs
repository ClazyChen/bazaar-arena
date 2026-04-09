using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class Incense
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "熏香",
            Desc = "▶减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；▶获得 {Regen} 生命再生",
            Cooldown = 6.0,
            Tags = 0,
            Slow = 1.0,
            SlowTargetCount = [1, 2, 3, 4],
            Regen = [2, 4, 6, 8],
            Abilities =
            [
                Ability.Slow,
                Ability.Regen.Override(priority: AbilityPriority.Low),
            ],
        };
    }
}

