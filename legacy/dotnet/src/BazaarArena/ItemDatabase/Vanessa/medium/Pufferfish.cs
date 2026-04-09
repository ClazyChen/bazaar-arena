using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Pufferfish
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "河豚",
            Desc = "▶ 造成 {Poison} 剧毒；触发加速时，为此物品充能 {ChargeSeconds} 秒",
            Cooldown = [8.0, 7.0, 6.0],
            Tags = Tag.Aquatic | Tag.Friend,
            Poison = [10, 20, 30],
            Charge = 2.0,
            Abilities =
            [
                Ability.Poison,
                Ability.Charge.Override(
                    trigger: Trigger.Haste,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Low),
            ],
        };
    }
}

