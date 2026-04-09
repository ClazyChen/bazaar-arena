using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

public static class SeadogsSaloon
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "海狗沙龙",
            Desc = "▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；▶ 加速 {HasteTargetCount} 件物品 {HasteSeconds} 秒；每有 1 件伙伴物品，此物品 +1 多重释放",
            Cooldown = [6.0, 5.0, 4.0],
            Tags = Tag.Aquatic | Tag.Property,
            SlowTargetCount = 1,
            Slow = 2.0,
            HasteTargetCount = 1,
            Haste = 2.0,
            Abilities =
            [
                Ability.Slow,
                Ability.Haste,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Value = Formula.Count(Condition.SameSide & Condition.WithTag(Tag.Friend)),
                }
            ],
        };
    }
}

