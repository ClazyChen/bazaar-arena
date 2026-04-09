using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class NoxiousPotion
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "剧毒药水",
            Desc = "▶造成 {Poison} 剧毒；弹药：{AmmoCap}",
            Cooldown = 4.0,
            Tags = Tag.Potion,
            Poison = [3, 6, 9, 12],
            AmmoCap = 1,
            Abilities =
            [
                Ability.Poison,
            ],
        };
    }
}

