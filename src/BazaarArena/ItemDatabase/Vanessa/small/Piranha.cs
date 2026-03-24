using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>食人鱼（Piranha）及其历史版本：海盗小型武器、水系、伙伴。</summary>
public static class Piranha
{
    /// <summary>食人鱼：铜、小；8 » 7 » 6 » 5s 武器 水系 伙伴；▶ 造成 20 伤害；使用其他伙伴或食物时，为此物品充能 1 秒。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "食人鱼",
            Desc = "▶ 造成 {Damage} 伤害；使用其他伙伴或食物时，为此物品充能 1 秒",
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Friend,
            Cooldown = [8.0, 7.0, 6.0, 5.0],
            Damage = 20,
            Charge = 1.0,
            Abilities =
            [
                Ability.Damage,
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.WithTag(Tag.Friend | Tag.Food),
                    targetCondition: Condition.SameAsCaster
                )
            ],
        };
    }

    /// <summary>食人鱼_S1（铜）：6s 小 铜 武器 水系 伙伴；▶ 造成 6 » 12 » 18 » 24 伤害；暴击率 20%；此物品能造成双倍暴击伤害。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "食人鱼_S1",
            Desc = "▶ 造成 {Damage} 伤害；暴击率：{CritRate%}；此物品能造成双倍暴击伤害",
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Friend,
            Cooldown = 6.0,
            Damage = [6, 12, 18, 24],
            CritRate = 20,
            Abilities =
            [
                Ability.Damage,
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
