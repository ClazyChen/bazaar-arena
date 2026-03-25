using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class DarkwaterAnglerfish
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "幽渊鮟鱇",
            Desc = "▶ 造成 {Burn} 灼烧；触发减速时，为此物品充能 {ChargeSeconds} 秒",
            Cooldown = [8.0, 7.0, 6.0],
            Tags = Tag.Aquatic | Tag.Friend,
            Burn = [10, 20, 30],
            Charge = 2.0,
            Abilities =
            [
                Ability.Burn.Override(priority: AbilityPriority.Low),
                Ability.Charge.Override(
                    trigger: Trigger.Slow,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Low),
            ],
        };
    }
}

