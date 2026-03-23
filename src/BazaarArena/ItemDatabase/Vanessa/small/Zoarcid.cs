using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>棉鳚（Zoarcid）及其历史版本：海盗小型武器、水系、伙伴。</summary>
public static class Zoarcid
{
    /// <summary>棉鳚：8 » 7 » 6 » 5s 小 铜 武器 水系 伙伴；▶ 造成 20 伤害；▶ 加速相邻物品 2 秒；触发灼烧时，为此物品充能 1 秒。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "棉鳚",
            Desc = "▶ 造成 {Damage} 伤害；▶ 加速相邻物品 {HasteSeconds} 秒；触发灼烧时，为此物品充能 1 秒",
            Tags = [Tag.Weapon, Tag.Aquatic, Tag.Friend],
            Cooldown = [8.0, 7.0, 6.0, 5.0],
            Damage = 20,
            Haste = 2.0,
            HasteTargetCount = 2,
            Charge = 1.0,
            Abilities =
            [
                Ability.Damage,
                Ability.Haste.Override(
                    additionalTargetCondition: Condition.AdjacentToCaster
                ),
                Ability.Charge.Override(
                    trigger: Trigger.Burn,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }

    /// <summary>棉鳚_S6（铜）：6s 小 铜 武器 水系 伙伴；▶ 造成 15 » 30 » 45 » 60 伤害；▶ 加速相邻物品 2 秒；触发灼烧时，为此物品充能 1 秒。</summary>
    public static ItemTemplate Template_S6()
    {
        return new ItemTemplate
        {
            Name = "棉鳚_S6",
            Desc = "▶ 造成 {Damage} 伤害；▶ 加速相邻物品 {HasteSeconds} 秒；触发灼烧时，为此物品充能 1 秒",
            Tags = [Tag.Weapon, Tag.Aquatic, Tag.Friend],
            Cooldown = 6.0,
            Damage = [15, 30, 45, 60],
            Haste = 2.0,
            HasteTargetCount = 2,
            Charge = 1.0,
            Abilities =
            [
                Ability.Damage,
                Ability.Haste.Override(
                    additionalTargetCondition: Condition.AdjacentToCaster
                ),
                Ability.Charge.Override(
                    trigger: Trigger.Burn,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }

    /// <summary>棉鳚_S4（铜）：7s 小 铜 武器 水系 伙伴；▶ 造成 20 » 30 » 40 » 50 伤害；▶ 加速相邻物品 1 » 2 » 3 » 4 秒；触发灼烧时，为此物品充能 1 秒。</summary>
    public static ItemTemplate Template_S4()
    {
        return new ItemTemplate
        {
            Name = "棉鳚_S4",
            Desc = "▶ 造成 {Damage} 伤害；▶ 加速相邻物品 {HasteSeconds} 秒；触发灼烧时，为此物品充能 1 秒",
            Tags = [Tag.Weapon, Tag.Aquatic, Tag.Friend],
            Cooldown = 7.0,
            Damage = [20, 30, 40, 50],
            Haste = [1.0, 2.0, 3.0, 4.0],
            HasteTargetCount = 2,
            Charge = 1.0,
            Abilities =
            [
                Ability.Damage,
                Ability.Haste.Override(
                    additionalTargetCondition: Condition.AdjacentToCaster
                ),
                Ability.Charge.Override(
                    trigger: Trigger.Burn,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }
}
