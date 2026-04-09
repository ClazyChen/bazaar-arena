using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class DockLines
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "码头缆索",
            Desc = "▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒",
            Cooldown = 4.0,
            Tags = Tag.Aquatic | Tag.Tool,
            SlowTargetCount = [2, 3, 4],
            Slow = 3.0,
            Abilities =
            [
                Ability.Slow,
            ],
        };
    }
}

