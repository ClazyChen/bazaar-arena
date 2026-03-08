using BazaarArena.Core;

namespace BazaarArena.ItemDatabase;

/// <summary>基座阶段测试用物品：纯伤害、纯灼烧、带暴击的伤害。</summary>
public static class TestItems
{
    /// <summary>测试伤害物品：冷却 2 秒，造成 30 点伤害。小型 1 槽。</summary>
    public static ItemTemplate TestDamage()
    {
        return new ItemTemplate
        {
            Name = "测试伤害",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = [],
            CooldownMs = 2000,
            CritRatePercent = 0,
            Multicast = 1,
            AmmoCap = 0,
            Abilities =
            [
                new()
                {
                    TriggerName = "使用物品",
                    Priority = AbilityPriority.Medium,
                    Effects = [new() { Kind = EffectKind.Damage, Value = 30 }],
                },
            ],
        };
    }

    /// <summary>测试灼烧物品：冷却 4 秒，附加 40 点灼烧。中型 2 槽。</summary>
    public static ItemTemplate TestBurn()
    {
        return new ItemTemplate
        {
            Name = "测试灼烧",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Medium,
            Tags = [],
            CooldownMs = 4000,
            CritRatePercent = 0,
            Multicast = 1,
            AmmoCap = 0,
            Abilities =
            [
                new()
                {
                    TriggerName = "使用物品",
                    Priority = AbilityPriority.Medium,
                    Effects = [new() { Kind = EffectKind.Burn, Value = 40 }],
                },
            ],
        };
    }

    /// <summary>测试暴击伤害：冷却 2 秒，50 伤害，50% 暴击率。大型 3 槽。</summary>
    public static ItemTemplate TestCritDamage()
    {
        return new ItemTemplate
        {
            Name = "测试暴击伤害",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Large,
            Tags = [],
            CooldownMs = 2000,
            CritRatePercent = 50,
            Multicast = 1,
            AmmoCap = 0,
            Abilities =
            [
                new()
                {
                    TriggerName = "使用物品",
                    Priority = AbilityPriority.Medium,
                    Effects = [new() { Kind = EffectKind.Damage, Value = 50 }],
                },
            ],
        };
    }

    /// <summary>伤害随等级：铜 25、银 35、金 45、钻石 55；冷却固定 2 秒。</summary>
    public static ItemTemplate TestDamageByTier()
    {
        return new ItemTemplate
        {
            Name = "伤害随等级",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = [],
            CooldownMs = 2000,
            CritRatePercent = 0,
            Multicast = 1,
            AmmoCap = 0,
            Damage = [25, 35, 45, 55],
            Abilities =
            [
                new()
                {
                    TriggerName = "使用物品",
                    Priority = AbilityPriority.Medium,
                    Effects = [new() { Kind = EffectKind.Damage, Value = 0 }],
                },
            ],
        };
    }

    /// <summary>冷却随等级：铜 3 秒、银 2.5 秒、金 2 秒、钻石 1.5 秒；伤害固定 30。中型 2 槽。</summary>
    public static ItemTemplate TestCooldownByTier()
    {
        return new ItemTemplate
        {
            Name = "冷却随等级",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Medium,
            Tags = [],
            CooldownMs = [3000, 2500, 2000, 1500],
            CritRatePercent = 0,
            Multicast = 1,
            AmmoCap = 0,
            Abilities =
            [
                new()
                {
                    TriggerName = "使用物品",
                    Priority = AbilityPriority.Medium,
                    Effects = [new() { Kind = EffectKind.Damage, Value = 30 }],
                },
            ],
        };
    }

    /// <summary>注册所有测试物品到数据库。</summary>
    public static void RegisterAll(ItemDatabase db)
    {
        db.Register(TestDamage());
        db.Register(TestBurn());
        db.Register(TestCritDamage());
        db.Register(TestDamageByTier());
        db.Register(TestCooldownByTier());
    }
}
