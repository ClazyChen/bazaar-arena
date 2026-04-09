using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class Thurible
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "香炉",
            Desc = "▶造成 {Burn} 灼烧；▶获得 {Regen} 生命再生",
            Cooldown = [6.0, 5.0, 4.0, 3.0],
            Tags = Tag.Tool | Tag.Relic,
            Burn = 5,
            Regen = 1,
            Abilities =
            [
                Ability.Burn,
                Ability.Regen,
            ],
        };
    }
}

