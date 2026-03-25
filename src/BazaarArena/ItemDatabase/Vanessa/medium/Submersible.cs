using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Submersible
{
    private static Formula HasOtherVehicleOrLarge { get; } =
        Formula.Apply(Formula.Count(Condition.SameSide & Condition.DifferentFromCaster & Condition.WithTag(Tag.Vehicle | Tag.Large)), n => n > 0 ? 1 : 0);

    public static ItemTemplate Template()
    {
        var aquaticWeapon = Condition.SameSide & Condition.WithTag(Tag.Aquatic) & Condition.WithTag(Tag.Weapon) & ~Condition.Destroyed;
        var aquaticShieldItem = Condition.SameSide & Condition.WithTag(Tag.Aquatic) & Condition.WithDerivedTag(DerivedTag.Shield) & ~Condition.Destroyed;

        return new ItemTemplate
        {
            Name = "深潜器",
            Desc = "▶ 己方最左侧的水系武器伤害提高 {Custom_0}（限本场战斗）；▶ 己方最右侧的水系武器伤害提高 {Custom_0}（限本场战斗）；▶ 己方最左侧的水系护盾物品护盾提高 {Custom_0}（限本场战斗）；▶ 己方最右侧的水系护盾物品护盾提高 {Custom_0}（限本场战斗）；如果有 1 件其他载具或大型物品，此物品的冷却时间缩短 2 秒",
            Cooldown = 5.0,
            Tags = Tag.Aquatic | Tag.Tool | Tag.Tech | Tag.Vehicle,
            Custom_0 = [10, 20, 30],
            Abilities =
            [
                Ability.AddAttribute(Key.Damage).Override(
                    targetCondition: Condition.LeftMost(aquaticWeapon),
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.High),
                Ability.AddAttribute(Key.Damage).Override(
                    targetCondition: Condition.RightMost(aquaticWeapon),
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.High),
                Ability.AddAttribute(Key.Shield).Override(
                    targetCondition: Condition.LeftMost(aquaticShieldItem),
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.High),
                Ability.AddAttribute(Key.Shield).Override(
                    targetCondition: Condition.RightMost(aquaticShieldItem),
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.High),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CooldownMs,
                    Condition = HasOtherVehicleOrLarge,
                    Value = Formula.Constant(-2000),
                }
            ]
        };
    }

    public static ItemTemplate Template_S8()
    {
        return new ItemTemplate
        {
            Name = "深潜器_S8",
            Desc = "▶ 造成 {Damage} 伤害；▶ 获得护盾，等量于此物品的伤害；如果有 1 件其他载具或大型物品，此物品的冷却时间缩短 2 秒",
            Cooldown = 5.0,
            Tags = Tag.Aquatic | Tag.Tool | Tag.Tech | Tag.Vehicle,
            Damage = [20, 40, 60],
            Shield = 0,
            Abilities =
            [
                Ability.Damage,
                Ability.Shield,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Shield,
                    Value = Formula.Caster(Key.Damage),
                },
                new AuraDefinition
                {
                    Attribute = Key.CooldownMs,
                    Condition = HasOtherVehicleOrLarge,
                    Value = Formula.Constant(-2000),
                }
            ]
        };
    }
}

