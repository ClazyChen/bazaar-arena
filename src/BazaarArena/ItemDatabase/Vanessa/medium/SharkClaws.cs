using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>鲨齿爪（Shark Claws）：海盗中型武器、水系。▶ 造成伤害；▶ 己方武器伤害提高（限本场战斗）。</summary>
public static class SharkClaws
{
    /// <summary>鲨齿爪（最新版）：6s 中 银 武器 水系；▶ 造成 10 » 20 » 30 » 40 伤害；▶ 己方武器伤害提高 10 » 20 » 30 » 40（限本场战斗）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "鲨齿爪",
            Desc = "▶ 造成 {Damage} 伤害；▶ 己方武器伤害提高 {Custom_0}（限本场战斗）",
            Tags = [Tag.Weapon, Tag.Aquatic],
            Cooldown = 6.0,
            Damage = [10, 20, 30, 40],
            Custom_0 = [10, 20, 30, 40],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    additionalTargetCondition: Condition.WithTag(Tag.Weapon),
                    priority: AbilityPriority.High
                ),
            ],
        };
    }

    /// <summary>鲨齿爪_S2（版本 2）：5s 中 铜 武器 水系；▶ 造成 10 » 20 » 30 » 40 伤害；▶ 己方其他武器伤害提高 10 » 20 » 30 » 40（限本场战斗）。</summary>
    public static ItemTemplate Template_S2()
    {
        return new ItemTemplate
        {
            Name = "鲨齿爪_S2",
            Desc = "▶ 造成 {Damage} 伤害；▶ 己方其他武器伤害提高 {Custom_0}（限本场战斗）",
            Tags = [Tag.Weapon, Tag.Aquatic],
            Cooldown = 5.0,
            Damage = [10, 20, 30, 40],
            Custom_0 = [10, 20, 30, 40],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    additionalTargetCondition: Condition.WithTag(Tag.Weapon) & Condition.DifferentFromCaster
                ),
            ],
        };
    }
}
