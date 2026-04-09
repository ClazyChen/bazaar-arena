using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class ScimitarOfTheDeep
{
    private static Formula ItemSameSideAsCaster { get; } = new(ctx => ctx.Item.SideIndex == ctx.Caster.SideIndex ? 1 : 0);
    private static Formula PoisonFromDamagePercent { get; } =
        RatioUtil.PercentFloor(Formula.Item(Key.Damage), Formula.Caster(Key.Custom_0));

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "深海弯刀",
            Desc = "造成 {Damage} 伤害；此物品暴击时，造成剧毒，等量于此物品伤害的 {Custom_0%}；此物品被加速时，剧毒物品的剧毒提高 {Custom_2}（限本场战斗）",
            Cooldown = 5.0,
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Relic,
            Damage = 20,
            Custom_0 = 50,
            Custom_1 = 0,
            Custom_2 = [3, 6, 9],
            Abilities =
            [
                Ability.Damage,
                Ability.Poison.Override(
                    trigger: Trigger.Crit,
                    condition: Condition.SameAsCaster,
                    valueKey: Key.Custom_1),
                Ability.AddAttribute(Key.Poison).Override(
                    trigger: Trigger.Haste,
                    additionalCondition: Condition.InvokeTargetSameAsCaster,
                    additionalTargetCondition: Condition.WithDerivedTag(DerivedTag.Poison),
                    valueKey: Key.Custom_2),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Custom_1,
                    Value = PoisonFromDamagePercent,
                },
            ],
        };
    }

    public static ItemTemplate Template_S9()
    {
        return new ItemTemplate
        {
            Name = "深海弯刀_S9",
            Desc = "造成 {Damage} 伤害；此物品暴击时，造成剧毒，等量于此物品伤害的 {Custom_0%}；此物品被加速时，剧毒物品的剧毒提高 {Custom_2}（限本场战斗）",
            Cooldown = 5.0,
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Relic,
            Damage = [20, 40, 60],
            Custom_0 = 20,
            Custom_1 = 0,
            Custom_2 = [2, 4, 6],
            Abilities =
            [
                Ability.Damage,
                Ability.Poison.Override(
                    trigger: Trigger.Crit,
                    condition: Condition.SameAsCaster,
                    valueKey: Key.Custom_1),
                Ability.AddAttribute(Key.Poison).Override(
                    trigger: Trigger.Haste,
                    additionalCondition: Condition.InvokeTargetSameAsCaster,
                    additionalTargetCondition: ItemSameSideAsCaster & Condition.WithDerivedTag(DerivedTag.Poison),
                    valueKey: Key.Custom_2),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Custom_1,
                    Value = PoisonFromDamagePercent,
                },
            ],
        };
    }
}

