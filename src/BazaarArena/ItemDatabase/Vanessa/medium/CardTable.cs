using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class CardTable
{
    private static Formula NonCompanionOtherCount { get; } =
        Formula.Count(Condition.SameSide & ~Condition.WithTag(Tag.Friend));

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "牌桌",
            Desc = "▶ 1 件伙伴物品的多重释放提高 {Custom_0}（限本场战斗）；▶ 每有 1 件非伙伴物品，此物品的冷却时间延长 1 秒（限本场战斗）",
            Cooldown = [6.0, 5.0, 4.0],
            Tags = 0,
            Custom_0 = 1,
            ModifyAttributeTargetCount = 1,
            Custom_1 = 0,
            Abilities =
            [
                Ability.AddAttribute(Key.Multicast).Override(
                    additionalTargetCondition: Condition.WithTag(Tag.Friend),
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low),
                Ability.AddAttribute(Key.CooldownMs).Override(
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_1,
                    priority: AbilityPriority.Low),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Custom_1,
                    Value = Formula.Constant(1000) * NonCompanionOtherCount,
                },
            ],
        };
    }

    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "牌桌_S1",
            Desc = "▶ 1 件伙伴物品的多重释放提高 {Custom_0}（限本场战斗）；▶ 此物品的冷却时间延长 {ChargeSeconds} 秒（限本场战斗）",
            Cooldown = 5.0,
            Tags = 0,
            Custom_0 = 1,
            Charge = [4.0, 3.0, 2.0],
            ModifyAttributeTargetCount = 1,
            Abilities =
            [
                Ability.AddAttribute(Key.Multicast).Override(
                    additionalTargetCondition: Condition.WithTag(Tag.Friend),
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low),
                Ability.AddAttribute(Key.CooldownMs).Override(
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Charge,
                    priority: AbilityPriority.Low),
            ],
        };
    }
}

