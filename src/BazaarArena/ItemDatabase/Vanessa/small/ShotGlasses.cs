using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>烈酒杯（Shot Glasses）及其历史版本：海盗小型；▶ 加速；▶ 减速（High）；弹药。</summary>
public static class ShotGlasses
{
    /// <summary>烈酒杯（版本 9，银）：3s 小 银；▶ 加速 4 件物品 1 秒；▶ 减速 4 件物品 1 秒（High）；弹药：1 » 2 » 3。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "烈酒杯",
            Desc = "▶ 加速 {HasteTargetCount} 件物品 {HasteSeconds} 秒；▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；弹药：{AmmoCap}",
            Tags = 0,
            Cooldown = 3.0,
            AmmoCap = [1, 2, 3],
            Haste = 1.0,
            HasteTargetCount = 4,
            Slow = 1.0,
            SlowTargetCount = 4,
            Abilities =
            [
                Ability.Haste,
                Ability.Slow.Override(priority: AbilityPriority.High),
            ],
        };
    }

    /// <summary>烈酒杯_S4（版本 4，银）：5s 小 银；▶ 加速己方物品 1 秒；▶ 减速己方物品 1 秒（High）；弹药：1 » 2 » 3。</summary>
    public static ItemTemplate Template_S4()
    {
        return new ItemTemplate
        {
            Name = "烈酒杯_S4",
            Desc = "▶ 加速己方物品 {HasteSeconds} 秒；▶ 减速己方物品 {SlowSeconds} 秒；弹药：{AmmoCap}",
            Tags = 0,
            Cooldown = 5.0,
            AmmoCap = [1, 2, 3],
            Haste = 1.0,
            Slow = 1.0,
            Abilities =
            [
                Ability.Haste,
                Ability.Slow.Override(
                    targetCondition: Condition.SameSide,
                    priority: AbilityPriority.High
                ),
            ],
        };
    }
}

