using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class OrangeJulian
{
    private static Formula BonusDamageFromTotalGold { get; } =
        RatioUtil.PercentFloor(Formula.Caster(Key.Custom_0) + Formula.Side(Key.Gold), 50);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "橘利安",
            Desc = "▶ 武器伤害提高，等量于本次冒险中总计获得金币的 50%（限本场战斗）",
            Cooldown = [10.0, 9.0, 8.0],
            Tags = Tag.Friend,
            Custom_0 = 0,
            Custom_1 = 0,
            OverridableAttributes = new Dictionary<int, IntOrByTier>
            {
                [Key.Custom_0] = 0,
            },
            Abilities =
            [
                Ability.AddAttribute(Key.Damage).Override(
                    valueKey: Key.Custom_1,
                    additionalTargetCondition: Condition.WithTag(Tag.Weapon)),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Custom_1,
                    Value = BonusDamageFromTotalGold,
                },
            ],
        };
    }

    public static ItemTemplate Template_S8()
    {
        return new ItemTemplate
        {
            Name = "橘利安_S8",
            Desc = "▶ 武器伤害提高 {Custom_0}（限本场战斗）；当你获得金币时，永久提高此物品的武器伤害提高量，等量于获得的数量",
            Cooldown = [10.0, 9.0, 8.0],
            Tags = Tag.Friend,
            Custom_0 = 10,
            Custom_1 = 0,
            Custom_2 = 0,
            OverridableAttributes = new Dictionary<int, IntOrByTier>
            {
                [Key.Custom_1] = 0,
            },
            Abilities =
            [
                Ability.AddAttribute(Key.Damage).Override(
                    valueKey: Key.Custom_2,
                    additionalTargetCondition: Condition.WithTag(Tag.Weapon)),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Custom_2,
                    Value = Formula.Caster(Key.Custom_0) + Formula.Caster(Key.Custom_1),
                }
            ]
        };
    }
}

