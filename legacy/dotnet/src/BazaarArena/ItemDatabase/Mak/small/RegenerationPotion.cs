using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class RegenerationPotion
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "再生药水",
            Desc = "▶获得 {Regen} 生命再生；弹药：{AmmoCap}",
            Cooldown = 5.0,
            Tags = Tag.Potion,
            Regen = [5, 10, 20, 30],
            AmmoCap = 1,
            Abilities =
            [
                Ability.Regen.Override(priority: AbilityPriority.High),
            ],
        };
    }
}

