using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class WeatherGlass
{
    private static Formula HasOtherWithDerivedTag(int derivedTag) =>
        Formula.Apply(Formula.Count(Condition.SameSide & Condition.DifferentFromCaster & Condition.WithDerivedTag(derivedTag)), n => n > 0 ? 1 : 0);

    private static Formula MulticastFromKeywords { get; } =
        HasOtherWithDerivedTag(DerivedTag.Burn)
        + HasOtherWithDerivedTag(DerivedTag.Poison)
        + HasOtherWithDerivedTag(DerivedTag.Slow)
        + HasOtherWithDerivedTag(DerivedTag.Freeze);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "风暴瓶",
            Desc = "▶ 造成 {Burn} 灼烧；▶ 造成 {Poison} 剧毒；如果有 1 件其他的灼烧/剧毒/减速/冻结物品，对每个词条，此物品 +1 多重释放",
            Cooldown = [7.0, 6.0, 5.0],
            Tags = Tag.Tool,
            Burn = 4,
            Poison = 4,
            Abilities =
            [
                Ability.Burn,
                Ability.Poison,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Value = MulticastFromKeywords,
                }
            ],
        };
    }

    public static ItemTemplate Template_S4()
    {
        return new ItemTemplate
        {
            Name = "风暴瓶_S4",
            Desc = "▶ 造成 {Burn} 灼烧；▶ 造成 {Poison} 剧毒；如果有 1 件其他的灼烧/剧毒/减速/冻结物品，对每个词条，此物品 +1 多重释放",
            Cooldown = 8.0,
            Tags = Tag.Tool,
            Burn = [4, 6, 8],
            Poison = [2, 3, 4],
            Abilities =
            [
                Ability.Burn,
                Ability.Poison,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Value = MulticastFromKeywords,
                }
            ],
        };
    }
}

