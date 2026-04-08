using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class Venomander
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "毒蜥",
            Desc = "▶造成 {Poison} 剧毒；▶获得 {Regen} 生命再生",
            Cooldown = [6.0, 5.0, 4.0, 3.0],
            Tags = Tag.Friend,
            Poison = 2,
            Regen = 1,
            Abilities =
            [
                Ability.Poison,
                Ability.Regen.Override(priority: AbilityPriority.Low),
            ],
        };
    }
}

