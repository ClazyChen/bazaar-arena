using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>舱底蠕虫（Bilge Worm）及其历史版本：一物品一文件，含 S9、S10 等。</summary>
public static class BilgeWorm
{
    /// <summary>舱底蠕虫：铜、小；敌人使用其最左侧的物品时，造成 10 » 20 » 30 » 40 伤害；吸血。标签：武器。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "舱底蠕虫",
            Desc = "敌人使用其最左侧的物品时，造成 {Damage} 伤害；吸血",
            Tags = [Tag.Weapon, Tag.Aquatic],
            Damage = [10, 20, 30, 40],
            LifeSteal = 1,
            Abilities =
            [
                Ability.Damage.Override(
                    trigger: Trigger.UseItem,
                    condition: Condition.DifferentSide & Condition.Leftmost
                ),
            ],
        };
    }

    /// <summary>舱底蠕虫_S10：5s 小；▶ 己方物品获得 4 » 8 » 12 暴击率（限本场战斗）；▶ 对自己造成 1 剧毒；造成暴击时，为此物品充能 1 秒</summary>
    public static ItemTemplate Template_S10()
    {
        return new ItemTemplate
        {
            Name = "舱底蠕虫_S10",
            Desc = "▶ 己方物品获得 {+Custom_0} 暴击率（限本场战斗）；▶ 对自己造成 {Poison} 剧毒；造成暴击时，为此物品充能 1 秒",
            Tags = [],
            Cooldown = 5.0,
            Poison = 1,
            Custom_0 = [4, 8, 12],
            Abilities =
            [
                Ability.AddAttribute(Key.CritRatePercent).Override(
                    priority: AbilityPriority.High
                ),
                Ability.PoisonSelf,
                Ability.Charge.Override(
                    trigger: Trigger.Crit,
                    targetCondition: Condition.SameAsSource
                ),
            ],
        };
    }

    /// <summary>舱底蠕虫_S9：4s 小；▶ 己方物品获得 4 » 8 » 12 暴击率（限本场战斗）；造成暴击时，对自己造成 1 剧毒</summary>
    public static ItemTemplate Template_S9()
    {
        return new ItemTemplate
        {
            Name = "舱底蠕虫_S9",
            Desc = "▶ 己方物品获得 {+Custom_0} 暴击率（限本场战斗）；造成暴击时，对自己造成 {Poison} 剧毒",
            Tags = [],
            Cooldown = 4.0,
            Poison = 1,
            Custom_0 = [4, 8, 12],
            Abilities =
            [
                Ability.AddAttribute(Key.CritRatePercent).Override(
                    priority: AbilityPriority.High
                ),
                Ability.PoisonSelf.Override(
                    trigger: Trigger.Crit
                ),
            ],
        };
    }
}
