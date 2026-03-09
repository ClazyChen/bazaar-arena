using BazaarArena.Core;

namespace BazaarArena.ItemDatabase;

/// <summary>公共物品模板：供卡组与模拟共用的常见物品。</summary>
public static class Common
{
    // 在此添加各公共物品的 ItemTemplate 工厂方法，并在 RegisterAll 中注册。

    /// <summary>獠牙：铜、小；冷却 3 秒，造成 5 » 10 » 15 » 20 伤害。标签：武器。</summary>
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

    /// <summary>岩浆核心：铜、小；每场战斗开始时造成 6 » 9 » 12 » 15 灼烧。</summary>
    public static ItemTemplate LavaCore()
    {
        return new ItemTemplate
        {
            Name = "岩浆核心",
            Desc = "每场战斗开始时，造成 {Burn} 灼烧",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = [],
            Burn = [6, 9, 12, 15],
            Abilities =
            [
                new()
                {
                    TriggerName = "战斗开始",
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Burn],
                },
            ],
        };
    }

    /// <summary>驯化蜘蛛：铜、小；冷却 6 秒，造成 1 » 2 » 3 » 4 剧毒。标签：伙伴。</summary>
    public static ItemTemplate TrainedSpider()
    {
        return new ItemTemplate
        {
            Name = "驯化蜘蛛",
            Desc = "造成 {Poison} 剧毒",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = ["伙伴"],
            Cooldown = 6.0,
            Poison = [1, 2, 3, 4],
            Abilities =
            [
                new()
                {
                    TriggerName = "使用物品",
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Poison],
                },
            ],
        };
    }

    /// <summary>举重手套（Lifting Gloves）：小、铜；冷却 5 秒，武器伤害提高 1 » 2 » 3 » 4（限本场战斗），优先级 High。</summary>
    public static ItemTemplate LiftingGloves()
    {
        return new ItemTemplate
        {
            Name = "举重手套",
            Desc = "武器伤害提高 {Custom_0}（限本场战斗）",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = ["工具", "服饰"],
            Cooldown = 5.0,
            Custom_0 = [1, 2, 3, 4],
            Abilities =
            [
                new()
                {
                    TriggerName = "使用物品",
                    Priority = AbilityPriority.High,
                    Effects = [Effect.WeaponDamageBonus(ValueKey: "Custom_0")],
                },
            ],
        };
    }

    /// <summary>注册所有公共物品到数据库。</summary>
    public static void RegisterAll(ItemDatabase db)
    {
        db.Register(Fang());
        db.Register(LavaCore());
        db.Register(TrainedSpider());
        db.Register(LiftingGloves());
    }
}
