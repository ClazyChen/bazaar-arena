using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class Sulphur
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "硫磺",
            Desc = "▶造成 {Burn} 灼烧",
            Cooldown = 6.0,
            Tags = Tag.Reagent,
            Burn = [2, 3, 4, 5],
            Abilities =
            [
                Ability.Burn,
            ],
        };
    }
}

