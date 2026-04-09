using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class Venom
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "毒液",
            Desc = "使用此物品左侧的物品时，造成 {Poison} 剧毒",
            Cooldown = 0.0,
            Tags = 0,
            Poison = [2, 3, 4, 5],
            Abilities =
            [
                Ability.Poison.Override(
                    trigger: Trigger.UseOtherItem,
                    condition: Condition.SameSide & Condition.LeftOfCaster
                ),
            ],
        };
    }
}

