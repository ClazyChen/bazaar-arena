using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>靴里剑（Shoe Blade）及其历史版本：海盗小型武器、服饰。</summary>
public static class ShoeBlade
{
    /// <summary>靴里剑（版本 11）：6s 小 铜 武器 服饰；▶ 造成 20 » 40 » 60 » 80 伤害；首次使用此物品时，暴击率 +100%（Custom_0=0 时光环生效，使用后 Custom_0 置 1 且不显示日志）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "靴里剑",
            Desc = "▶ 造成 {Damage} 伤害；首次使用此物品时，暴击率 +100%",
            Tags = [Tag.Weapon, Tag.Apparel],
            Cooldown = 6.0,
            Damage = [20, 40, 60, 80],
            Custom_0 = 0,
            Custom_1 = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Custom_0).Override(
                    additionalCondition: Condition.CasterCustom0IsZero,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_1,
                    effectLogName: "",
                    priority: AbilityPriority.Lowest
                ),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Value = Formula.Constant(100),
                    Condition = Condition.SameAsCaster & Condition.CasterCustom0IsZero,
                },
            ],
        };
    }

    /// <summary>靴里剑_S1（版本 1）：7s 小 铜 武器 服饰；▶ 造成 20 » 40 » 60 » 80 伤害；暴击率 15 » 30 » 50 » 100%。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "靴里剑_S1",
            Desc = "▶ 造成 {Damage} 伤害；暴击率：{CritRate}%",
            Tags = [Tag.Weapon, Tag.Apparel],
            Cooldown = 7.0,
            Damage = [20, 40, 60, 80],
            CritRate = [15, 30, 50, 100],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }
}
