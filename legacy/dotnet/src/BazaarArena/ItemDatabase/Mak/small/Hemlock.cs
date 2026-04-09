using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class Hemlock
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "毒芹",
            Desc = "▶造成 {Poison} 剧毒",
            Cooldown = 6.0,
            Tags = Tag.Reagent,
            Poison = [2, 3, 4, 5],
            Abilities =
            [
                Ability.Poison,
            ],
        };
    }
}

