using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

public static class Port
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "港口",
            Desc = "▶ 为弹药物品装填 {Reload} 弹药（限本场战斗）；▶ 为弹药物品充能 {ChargeSeconds} 秒（限本场战斗）；每天开始时，获得 1 件任意英雄的小型弹药物品",
            Cooldown = 6.0,
            Tags = Tag.Aquatic | Tag.Property,
            Reload = [2, 3, 4],
            Charge = [1.0, 2.0, 3.0],
            Abilities =
            [
                Ability.Reload.Override(
                    additionalTargetCondition: Condition.WithDerivedTag(DerivedTag.Ammo),
                    priority: AbilityPriority.Low),
                Ability.Charge.Override(
                    additionalTargetCondition: Condition.WithDerivedTag(DerivedTag.Ammo)),
            ],
        };
    }

    public static ItemTemplate Template_S10()
    {
        return new ItemTemplate
        {
            Name = "港口_S10",
            Desc = "▶ 为弹药物品装填 {Reload} 弹药（限本场战斗）；▶ 为弹药物品充能 {ChargeSeconds} 秒（限本场战斗）；每天开始时，获得 1 件任意英雄的小型弹药物品",
            Cooldown = [6.0, 5.0, 4.0],
            Tags = Tag.Aquatic | Tag.Property,
            Reload = [2, 3, 4],
            Charge = 1.0,
            Abilities =
            [
                Ability.Reload.Override(
                    additionalTargetCondition: Condition.WithDerivedTag(DerivedTag.Ammo),
                    priority: AbilityPriority.Low),
                Ability.Charge.Override(
                    additionalTargetCondition: Condition.WithDerivedTag(DerivedTag.Ammo)),
            ],
        };
    }
}

