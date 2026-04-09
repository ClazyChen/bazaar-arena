using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Torpedo
{
    private static Formula AquaticOrAmmoUsed { get; } =
        Condition.WithTag(Tag.Aquatic) | Condition.WithDerivedTag(DerivedTag.Ammo);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "鱼雷",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；使用其他水系或弹药物品时，此物品伤害提高 {Custom_0}（限本场战斗）；如果该物品是大型，此物品伤害额外提高 {Custom_0}（限本场战斗）",
            Cooldown = 8.0,
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Tech,
            Damage = 100,
            AmmoCap = 1,
            Custom_0 = [40, 80, 120],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: AquaticOrAmmoUsed,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0),
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: AquaticOrAmmoUsed & Condition.WithTag(Tag.Large),
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0),
            ],
        };
    }

    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "鱼雷_S1",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；使用其他水系或弹药物品时，此物品伤害提高 {Custom_0}（限本场战斗）；如果该物品是大型，为此物品装填 {Reload} 发弹药",
            Cooldown = 8.0,
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Tech,
            Damage = 100,
            AmmoCap = 1,
            Reload = 1,
            Custom_0 = [30, 60, 90],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: AquaticOrAmmoUsed,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0),
                Ability.Reload.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: AquaticOrAmmoUsed & Condition.WithTag(Tag.Large),
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Lowest),
            ],
        };
    }
}

