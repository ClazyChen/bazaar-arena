using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class SmellingSalts
{
    private static Formula SelfOrAdjacent { get; } =
        Condition.SameAsCaster | Condition.AdjacentToCaster;

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "嗅盐",
            Desc = "▶减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；此物品或相邻物品触发减速时，加速此物品左侧的物品 {HasteSeconds} 秒",
            Cooldown = 7.0,
            Tags = 0,
            Slow = [1.0, 2.0, 3.0, 4.0],
            SlowTargetCount = 1,
            Haste = [1.0, 2.0, 3.0, 4.0],
            Abilities =
            [
                Ability.Slow,
                Ability.Haste.Override(
                    trigger: Trigger.Slow,
                    additionalCondition: SelfOrAdjacent,
                    targetCondition: Condition.SameSide & Condition.LeftOfCaster & Condition.HasCooldown
                ),
            ],
        };
    }
}

