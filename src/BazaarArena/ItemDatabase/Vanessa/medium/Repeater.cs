using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Repeater
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "连发步枪",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；使用其他弹药物品时，使用此物品",
            Cooldown = 5.0,
            Tags = Tag.Weapon,
            Damage = 30,
            AmmoCap = [2, 3, 4],
            Abilities =
            [
                Ability.Damage,
                Ability.UseThisItem.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.WithDerivedTag(DerivedTag.Ammo)),
            ],
        };
    }

    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "连发步枪_S1",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；使用其他弹药物品时，为此物品充能 {ChargeSeconds} 秒",
            Cooldown = 7.0,
            Tags = Tag.Weapon,
            Damage = [60, 90, 120],
            AmmoCap = 6,
            Charge = [1.0, 2.0, 3.0],
            Abilities =
            [
                Ability.Damage,
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.WithDerivedTag(DerivedTag.Ammo),
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Low),
            ],
        };
    }
}

