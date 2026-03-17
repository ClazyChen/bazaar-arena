using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>宠物石（Pet Rock）：海盗小型武器、伙伴、玩具；造成 8 » 16 » 24 » 32 伤害；若此为唯一伙伴，己方物品暴击率 +10% » +15% » +20% » +25%。</summary>
public static class PetRock
{
    /// <summary>宠物石：6s 小 铜 武器 伙伴 玩具；▶ 造成 8 » 16 » 24 » 32 伤害；若此为唯一伙伴，己方物品暴击率 +10% » +15% » +20% » +25%。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "宠物石",
            Desc = "▶ 造成 {Damage} 伤害；若此为唯一伙伴，己方物品暴击率 {+Custom_0%}",
            Tags = [Tag.Weapon, Tag.Friend, Tag.Toy],
            Cooldown = 6.0,
            Damage = [8, 16, 24, 32],
            Custom_0 = [10, 15, 20, 25],
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.CritRatePercent,
                    Condition = Condition.SameSide,
                    SourceCondition = Condition.OnlyCompanion,
                    Value = Formula.Source(Key.Custom_0),
                },
            ],
            DownstreamRequirements =
            [
                Synergy.And(Tag.Crit),
            ],
        };
    }
}
