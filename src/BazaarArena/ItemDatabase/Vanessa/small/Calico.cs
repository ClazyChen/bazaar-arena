using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>三花（Calico）及其历史版本：海盗小型武器、伙伴。</summary>
public static class Calico
{
    /// <summary>三花：铜、小；6s 武器 伙伴；▶ 造成 15 » 30 » 45 » 60 伤害；使用其他武器时，此物品暴击率提高 +5 » +10 » +15 » +20%（限本场战斗）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "三花",
            Desc = "▶ 造成 {Damage} 伤害；使用其他武器时，此物品暴击率提高 {+Custom_0%}（限本场战斗）",
            Tags = [Tag.Weapon, Tag.Friend],
            Cooldown = 6.0,
            Damage = [15, 30, 45, 60],
            Custom_0 = [5, 10, 15, 20],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.CritRate).Override(
                    condition: Condition.SameSide & Condition.WithTag(Tag.Weapon) & Condition.DifferentFromCaster,
                    targetCondition: Condition.SameAsCaster
                ),
            ],
        };
    }

    /// <summary>三花_S1：铜、小；7s 武器 伙伴；▶ 造成 15 » 30 » 45 » 60 伤害；使用其他武器时此物品暴击率 +5 » +10 » +15 » +20%（限本场）；此物品能造成双倍暴击伤害。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "三花_S1",
            Desc = "▶ 造成 {Damage} 伤害；使用其他武器时，此物品暴击率提高 {+Custom_0%}（限本场战斗）；此物品能造成双倍暴击伤害",
            Tags = [Tag.Weapon, Tag.Friend],
            Cooldown = 7.0,
            Damage = [15, 30, 45, 60],
            Custom_0 = [5, 10, 15, 20],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.CritRate).Override(
                    condition: Condition.SameSide & Condition.WithTag(Tag.Weapon) & Condition.DifferentFromCaster,
                    targetCondition: Condition.SameAsCaster
                ),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritDamage,
                    Value = Formula.Constant(100),
                    Percent = true,
                },
            ],
        };
    }
}
