using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>手斧（Handaxe）：海盗小型武器；造成 10 » 15 » 20 » 25 伤害；己方武器伤害 +6 » +9 » +12 » +15（限本场战斗）。</summary>
public static class Handaxe
{
    /// <summary>手斧：6s 小 铜 武器；▶ 造成 10 » 15 » 20 » 25 伤害；己方武器伤害 +6 » +9 » +12 » +15（限本场战斗）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "手斧",
            Desc = "▶ 造成 {Damage} 伤害；己方武器伤害 +{Custom_0}（限本场战斗）",
            Tags = [Tag.Weapon],
            Cooldown = 6.0,
            Damage = [10, 15, 20, 25],
            Custom_0 = [6, 9, 12, 15],
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.Damage,
                    Condition = Condition.SameSide & Condition.WithTag(Tag.Weapon),
                    Value = Formula.Source(Key.Custom_0),
                },
            ],
            DownstreamRequirements =
            [
                Synergy.And(Tag.Weapon),
            ],
        };
    }
}
