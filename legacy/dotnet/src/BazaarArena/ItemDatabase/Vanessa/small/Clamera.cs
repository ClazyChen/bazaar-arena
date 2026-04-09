using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>拍立蚌（Clamera）及其历史版本：海盗小型水系；▶ 减速；部分版本在战斗开始或敌人前 N 次使用物品时自动触发。</summary>
public static class Clamera
{
    /// <summary>拍立蚌（版本 12，银）：7 » 6 » 5s 小 银 水系；▶ 减速 1 件物品 2 秒；每场战斗敌人前 2 » 3 » 4 次使用物品时，使用此物品。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "拍立蚌",
            Desc = "▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；每场战斗敌人前 {Custom_0} 次使用物品时，使用此物品",
            Tags = Tag.Aquatic,
            Cooldown = [7.0, 6.0, 5.0],
            Charge = 99.0,
            Slow = 2.0,
            SlowTargetCount = 1,
            Custom_0 = [2, 3, 4],
            Custom_1 = 1,
            Abilities =
            [
                Ability.Slow,
                Ability.ReduceAttribute(Key.Custom_0).Override(
                    trigger: Trigger.UseOtherItem,
                    condition: Condition.DifferentSide,
                    additionalCondition: Formula.Caster(Key.Custom_0),
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_1,
                    effectLogName: ""
                ),
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    condition: Condition.DifferentSide,
                    additionalCondition: Formula.Caster(Key.Custom_0),
                    targetCondition: Condition.SameAsCaster,
                    effectLogName: ""
                ),
            ],
        };
    }

    /// <summary>拍立蚌_S8（版本 8，铜）：7 » 6 » 5 » 4s 小 铜 水系；▶ 减速 1 件物品 2 秒；战斗开始时，使用此物品。</summary>
    public static ItemTemplate Template_S8()
    {
        return new ItemTemplate
        {
            Name = "拍立蚌_S8",
            Desc = "▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；战斗开始时，使用此物品",
            Tags = Tag.Aquatic,
            Cooldown = [7.0, 6.0, 5.0, 4.0],
            Charge = 99.0,
            Slow = 2.0,
            SlowTargetCount = 1,
            Abilities =
            [
                Ability.Slow,
                Ability.Charge.Override(
                    trigger: Trigger.BattleStart,
                    targetCondition: Condition.SameAsCaster,
                    effectLogName: ""
                ),
            ],
        };
    }

    /// <summary>拍立蚌_S1（版本 1，铜）：10s 小 铜 水系；▶ 减速 1 » 2 » 3 » 4 件物品 3 秒；战斗开始时，使用此物品。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "拍立蚌_S1",
            Desc = "▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；战斗开始时，使用此物品",
            Tags = Tag.Aquatic,
            Cooldown = 10.0,
            Charge = 99.0,
            Slow = 3.0,
            SlowTargetCount = [1, 2, 3, 4],
            Abilities =
            [
                Ability.Slow,
                Ability.Charge.Override(
                    trigger: Trigger.BattleStart,
                    targetCondition: Condition.SameAsCaster,
                    effectLogName: ""
                ),
            ],
        };
    }
}

