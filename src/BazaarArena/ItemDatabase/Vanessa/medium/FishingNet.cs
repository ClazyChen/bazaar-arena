using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>渔网（Fishing Net）：海盗中型水系、工具。▶ 减速若干件敌人物品。表格中「每天开始时……」为局外成长，不实现。</summary>
public static class FishingNet
{
    /// <summary>渔网（最新版，对应表格版本 12）：6s 中 铜 水系 工具；▶ 减速 1 » 2 » 3 » 4 件物品 2 秒。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "渔网",
            Desc = "▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒",
            Tags = [Tag.Aquatic, Tag.Tool],
            Cooldown = 6.0,
            Slow = 2.0,
            SlowTargetCount = [1, 2, 3, 4],
            Abilities =
            [
                Ability.Slow,
            ],
        };
    }

    /// <summary>渔网_S7（版本 7）：6s 中 铜 水系 工具；▶ 减速 1 » 2 » 3 » 4 件物品 2 秒。</summary>
    public static ItemTemplate Template_S7()
    {
        return new ItemTemplate
        {
            Name = "渔网_S7",
            Desc = "▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒",
            Tags = [Tag.Aquatic, Tag.Tool],
            Cooldown = 6.0,
            Slow = 2.0,
            SlowTargetCount = [1, 2, 3, 4],
            Abilities =
            [
                Ability.Slow,
            ],
        };
    }

    /// <summary>渔网_S1（版本 1）：9s 中 铜 水系 工具；▶ 减速 1 » 2 » 3 » 4 件物品 3 秒。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "渔网_S1",
            Desc = "▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒",
            Tags = [Tag.Aquatic, Tag.Tool],
            Cooldown = 9.0,
            Slow = 3.0,
            SlowTargetCount = [1, 2, 3, 4],
            Abilities =
            [
                Ability.Slow,
            ],
        };
    }
}
