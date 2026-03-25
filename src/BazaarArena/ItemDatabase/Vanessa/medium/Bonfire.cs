using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Bonfire
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "篝火",
            Desc = "造成 {Burn} 灼烧；触发灼烧时，加速 {HasteTargetCount} 件相邻物品 {Haste} 秒",
            Cooldown = 5.0,
            Tags = 0,
            Burn = [5, 10, 15],
            Haste = [1, 2, 3],
            Abilities =
            [
                Ability.Burn,
                Ability.Haste.Override(
                    trigger: Trigger.Burn,
                    additionalTargetCondition: Condition.AdjacentToCaster,
                    priority: AbilityPriority.High),
            ],
        };
    }
}

