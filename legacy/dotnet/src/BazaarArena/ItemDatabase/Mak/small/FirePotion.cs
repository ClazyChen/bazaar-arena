using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class FirePotion
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "火焰药水",
            Desc = "▶造成 {Burn} 灼烧；弹药：{AmmoCap}",
            Cooldown = 5.0,
            Tags = Tag.Potion,
            AmmoCap = 1,
            Burn = [6, 8, 10, 12],
            Abilities =
            [
                Ability.Burn,
            ],
        };
    }
}

