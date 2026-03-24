using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>弹簧刀（Switchblade）及其历史版本：海盗小型武器；使用相邻武器时使其伤害提高（限本场战斗）。</summary>
public static class Switchblade
{
    /// <summary>弹簧刀（版本 5）：4s 小 铜 武器；▶ 造成 4 » 8 » 12 » 16 伤害；使用相邻武器时，使其伤害提高 4 » 8 » 12 » 16（限本场战斗）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "弹簧刀",
            Desc = "▶ 造成 {Damage} 伤害；使用相邻武器时，使其伤害提高 {Custom_0}（限本场战斗）",
            Tags = Tag.Weapon,
            Cooldown = 4.0,
            Damage = [4, 8, 12, 16],
            Custom_0 = [4, 8, 12, 16],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    condition: Condition.SameSide & Condition.AdjacentToCaster & Condition.WithTag(Tag.Weapon) & Condition.DifferentFromCaster,
                    additionalTargetCondition: Condition.SameAsInvokeTarget,
                    valueKey: Key.Custom_0
                ),
            ],
        };
    }

    /// <summary>弹簧刀_S1（版本 1）：9s 小 铜 武器；▶ 造成 30 » 45 » 60 » 75 伤害；使用相邻武器时，使其伤害提高 3 » 6 » 9 » 12（限本场战斗）。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "弹簧刀_S1",
            Desc = "▶ 造成 {Damage} 伤害；使用相邻武器时，使其伤害提高 {Custom_0}（限本场战斗）",
            Tags = Tag.Weapon,
            Cooldown = 9.0,
            Damage = [30, 45, 60, 75],
            Custom_0 = [3, 6, 9, 12],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    condition: Condition.SameSide & Condition.AdjacentToCaster & Condition.WithTag(Tag.Weapon) & Condition.DifferentFromCaster,
                    additionalTargetCondition: Condition.SameAsInvokeTarget,
                    valueKey: Key.Custom_0
                ),
            ],
        };
    }
}
