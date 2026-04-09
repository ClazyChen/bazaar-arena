using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Harpoon
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "鱼叉",
            Desc = "▶ 摧毁 {DestroyTargetCount} 件小型物品；弹药：{AmmoCap}",
            Cooldown = [6.0, 5.0, 4.0],
            Tags = Tag.Aquatic,
            AmmoCap = [1, 2, 3],
            DestroyTargetCount = 1,
            Abilities =
            [
                Ability.Destroy.Override(
                    additionalTargetCondition: Condition.WithTag(Tag.Small),
                    priority: AbilityPriority.Highest),
            ],
        };
    }
}

