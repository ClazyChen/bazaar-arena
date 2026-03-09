using BazaarArena.Core;

namespace BazaarArena.ItemDatabase;

/// <summary>公共物品模板：供卡组与模拟共用的常见物品。</summary>
public static class Common
{
    // 在此添加各公共物品的 ItemTemplate 工厂方法，并在 RegisterAll 中注册。

    /// <summary>獠牙：冷却 3 秒，标签「武器」，伤害铜 5 / 银 10 / 金 15 / 钻石 20。</summary>
    public static ItemTemplate Fang()
    {
        return new ItemTemplate
        {
            Name = "獠牙",
            Desc = "造成 {Damage} 伤害",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = ["武器"],
            Cooldown = 3.0,
            Damage = [5, 10, 15, 20],
            Abilities =
            [
                new()
                {
                    TriggerName = "使用物品",
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Damage],
                },
            ],
        };
    }

    /// <summary>注册所有公共物品到数据库。</summary>
    public static void RegisterAll(ItemDatabase db)
    {
        db.Register(Fang());
    }
}
