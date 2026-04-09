using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

public static class DartLauncher
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "飞镖发射器",
            Desc = "造成 {Damage} 伤害；如果此物品与减速物品相邻，减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；如果此物品与剧毒物品相邻，造成 {Poison} 剧毒；弹药：{AmmoCap}",
            Cooldown = 4.0,
            Tags = Tag.Weapon | Tag.Tech,
            Damage = [5, 10, 15],
            Slow = 1.0,
            SlowTargetCount = [1, 2, 3],
            Poison = [3, 6, 9],
            AmmoCap = 4,
            Abilities =
            [
                Ability.Damage,
                Ability.Slow.Override(additionalCondition:
                    Formula.Count(Condition.SameSide & Condition.AdjacentToCaster & Condition.WithDerivedTag(DerivedTag.Slow))),
                Ability.Poison.Override(additionalCondition:
                    Formula.Count(Condition.SameSide & Condition.AdjacentToCaster & Condition.WithDerivedTag(DerivedTag.Poison))),
            ],
        };
    }
}

