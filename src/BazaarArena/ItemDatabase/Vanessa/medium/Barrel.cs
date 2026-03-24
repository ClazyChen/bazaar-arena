using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>木桶（Barrel）：海盗中型。▶ 获得护盾；使用相邻物品时此物品护盾提高（限本场战斗）。</summary>
public static class Barrel
{
    /// <summary>木桶（最新版）：6s 中 铜；▶ 获得 20 护盾；▶ 使用相邻物品时，此物品的护盾提高 10 » 20 » 30 » 40（限本场战斗）（High）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "木桶",
            Desc = "▶ 获得 {Shield} 护盾；▶ 使用相邻物品时，此物品的护盾提高 {Custom_0}（限本场战斗）",
            Tags = 0,
            Cooldown = 6.0,
            Shield = 20,
            Custom_0 = [10, 20, 30, 40],
            Abilities =
            [
                Ability.Shield,
                Ability.AddAttribute(Key.Shield).Override(
                    condition: Condition.SameSide & Condition.AdjacentToCaster,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.High
                ),
            ],
        };
    }

    /// <summary>木桶_S1（版本 1）：5s 中 铜；▶ 获得 20 护盾；▶ 使用相邻物品时，此物品的护盾提高 10 » 15 » 20 » 25（限本场战斗）（High）。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "木桶_S1",
            Desc = "▶ 获得 {Shield} 护盾；▶ 使用相邻物品时，此物品的护盾提高 {Custom_0}（限本场战斗）",
            Tags = 0,
            Cooldown = 5.0,
            Shield = 20,
            Custom_0 = [10, 15, 20, 25],
            Abilities =
            [
                Ability.Shield,
                Ability.AddAttribute(Key.Shield).Override(
                    condition: Condition.SameSide & Condition.AdjacentToCaster,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.High
                ),
            ],
        };
    }
}
