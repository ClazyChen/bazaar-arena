using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class Mothmeal
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "飞蛾粉末",
            Desc = "▶减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒",
            Cooldown = 5.0,
            Tags = Tag.Reagent,
            Slow = [1.0, 2.0, 3.0, 4.0],
            SlowTargetCount = 1,
            Abilities =
            [
                Ability.Slow.Override(targetCountKey: Key.SlowTargetCount),
            ],
        };
    }
}

