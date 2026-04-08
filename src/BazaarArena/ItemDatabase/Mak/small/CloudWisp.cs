using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class CloudWisp
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "云精灵",
            Desc = "▶加速此物品左侧的物品 {HasteSeconds} 秒",
            Cooldown = 5.0,
            Tags = Tag.Reagent,
            Haste = [1.0, 2.0, 3.0, 4.0],
            Abilities =
            [
                Ability.Haste.Override(
                    targetCondition: Condition.SameSide & Condition.LeftOfCaster & Condition.HasCooldown
                ),
            ],
        };
    }
}

