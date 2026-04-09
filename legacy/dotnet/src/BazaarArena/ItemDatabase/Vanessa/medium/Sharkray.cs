using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Sharkray
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "鳐鲨",
            Desc = "造成 {Damage} 伤害；己方伙伴被加速时，己方伙伴武器伤害提高 {Custom_0}（限本场战斗）；己方伙伴被加速时，己方伙伴的剧毒提高 {Custom_1}（限本场战斗）",
            Cooldown = 6.0,
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Friend | Tag.Ray,
            Damage = 20,
            Custom_0 = [5, 10, 15],
            Custom_1 = [1, 2, 3],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Haste,
                    additionalCondition: Condition.InvokeTargetSameSide & Condition.InvokeTargetWithTag(Tag.Friend),
                    additionalTargetCondition: Condition.WithTag(Tag.Friend) & Condition.WithTag(Tag.Weapon),
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low),
                Ability.AddAttribute(Key.Poison).Override(
                    trigger: Trigger.Haste,
                    additionalCondition: Condition.InvokeTargetSameSide & Condition.InvokeTargetWithTag(Tag.Friend),
                    additionalTargetCondition: Condition.WithTag(Tag.Friend) & Condition.WithDerivedTag(DerivedTag.Poison),
                    valueKey: Key.Custom_1,
                    priority: AbilityPriority.Low),
            ],
        };
    }

    public static ItemTemplate Template_S11()
    {
        return new ItemTemplate
        {
            Name = "鳐鲨_S11",
            Desc = "造成 {Damage} 伤害；触发加速时，此物品伤害提高 {Custom_0}（限本场战斗）",
            Cooldown = 6.0,
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Friend | Tag.Ray,
            Damage = 20,
            Custom_0 = [10, 20, 30, 40],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Haste,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low),
            ],
        };
    }
}

