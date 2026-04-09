using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

public static class Flagship
{
    private static Formula HasOtherWithTag(int tagMask) =>
        Formula.Apply(Formula.Count(Condition.SameSide & Condition.DifferentFromCaster & Condition.WithTag(tagMask)), n => n > 0 ? 1 : 0);

    private static Formula HasOtherAmmoItem { get; } =
        Formula.Apply(Formula.Count(Condition.SameSide & Condition.DifferentFromCaster & Condition.WithDerivedTag(DerivedTag.Ammo)), n => n > 0 ? 1 : 0);

    private static Formula MulticastFromKeywords { get; } =
        HasOtherWithTag(Tag.Tool)
        + HasOtherWithTag(Tag.Property)
        + HasOtherWithTag(Tag.Friend)
        + HasOtherAmmoItem
        + HasOtherWithTag(Tag.Relic);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "旗舰",
            Desc = "▶ 造成 {Damage} 伤害；如果有 1 件其他的工具/地产/伙伴/弹药/遗物物品，对每个词条，此物品 +1 多重释放",
            Cooldown = [7.0, 6.0, 5.0],
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Vehicle,
            Damage = 50,
            Abilities =
            [
                Ability.Damage,
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

