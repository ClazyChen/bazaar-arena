using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>淬锋钢（Honing Steel）及其历史版本：海盗小型工具。</summary>
public static class HoningSteel
{
    /// <summary>淬锋钢：3s 小 铜 工具；己方最左侧和最右侧的武器伤害提高 5 » 10 » 15 » 20（限本场战斗）（High）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "淬锋钢",
            Desc = "▶ 己方最左侧和最右侧的武器伤害提高 {Custom_0}（限本场战斗）",
            Tags = [Tag.Tool],
            Cooldown = 3.0,
            Custom_0 = [5, 10, 15, 20],
            Abilities =
            [
                Ability.AddAttribute(Key.Damage).Override(
                    additionalTargetCondition: Condition.LeftMost(Condition.WithTag(Tag.Weapon)),
                    priority: AbilityPriority.High
                ),
                Ability.AddAttribute(Key.Damage).Override(
                    additionalTargetCondition: Condition.RightMost(Condition.WithTag(Tag.Weapon)),
                    priority: AbilityPriority.High
                ),
            ],
            DownstreamRequirements =
            [
                Synergy.And(Tag.Weapon),
            ],
        };
    }

    /// <summary>淬锋钢_S9：4s 小 铜 工具；此物品右侧的武器伤害提高 6 » 12 » 18 » 24（限本场战斗）（High）。</summary>
    public static ItemTemplate Template_S9()
    {
        return new ItemTemplate
        {
            Name = "淬锋钢_S9",
            Desc = "▶ 此物品右侧的武器伤害提高 {Custom_0}（限本场战斗）",
            Tags = [Tag.Tool],
            Cooldown = 4.0,
            Custom_0 = [6, 12, 18, 24],
            Abilities =
            [
                Ability.AddAttribute(Key.Damage).Override(
                    additionalTargetCondition: Condition.RightOfSource & Condition.WithTag(Tag.Weapon),
                    priority: AbilityPriority.High
                ),
            ],
            DownstreamRequirements =
            [
                Synergy.And(SynergyDirection.Right, Tag.Weapon),
            ],
        };
    }

    /// <summary>淬锋钢_S1：5s 小 铜 工具；此物品右侧的武器伤害提高 8 » 12 » 16 » 20（限本场战斗）（High）。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "淬锋钢_S1",
            Desc = "▶ 此物品右侧的武器伤害提高 {Custom_0}（限本场战斗）",
            Tags = [Tag.Tool],
            Cooldown = 5.0,
            Custom_0 = [8, 12, 16, 20],
            Abilities =
            [
                Ability.AddAttribute(Key.Damage).Override(
                    additionalTargetCondition: Condition.RightOfSource & Condition.WithTag(Tag.Weapon),
                    priority: AbilityPriority.High
                ),
            ],
            DownstreamRequirements =
            [
                Synergy.And(SynergyDirection.Right, Tag.Weapon),
            ],
        };
    }
}
