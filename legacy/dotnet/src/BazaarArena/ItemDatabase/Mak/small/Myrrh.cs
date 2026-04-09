using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class Myrrh
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "没药",
            Desc = "▶获得 {Regen} 生命再生",
            Cooldown = 6.0,
            Tags = Tag.Reagent,
            Regen = [1, 3, 5, 7],
            Abilities =
            [
                Ability.Regen,
            ],
        };
    }
}

