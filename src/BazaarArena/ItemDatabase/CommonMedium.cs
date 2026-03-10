using BazaarArena.Core;

namespace BazaarArena.ItemDatabase;

/// <summary>公共中物品模板：供卡组与模拟共用的常见中型物品。</summary>
public static class CommonMedium
{
    /// <summary>尖刺圆盾：9s 中 铜 武器，造成 10 » 20 » 40 » 80 伤害；获得 10 » 20 » 40 » 80 护盾。</summary>
    public static ItemTemplate SpikedBuckler()
    {
        return new ItemTemplate
        {
            Name = "尖刺圆盾",
            Desc = "造成 {Damage} 伤害；获得 {Shield} 护盾",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Medium,
            Tags = [Tag.Weapon],
            Cooldown = 9.0,
            Damage = [10, 20, 40, 80],
            Shield = [10, 20, 40, 80],
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Damage, Effect.Shield],
                },
            ],
        };
    }

    /// <summary>临时钝器（Improvised Bludgeon）：8s 中 铜 武器，造成 20 » 40 » 80 » 160 伤害；减速 2 件物品 3 » 4 » 5 » 6 秒。</summary>
    public static ItemTemplate ImprovisedBludgeon()
    {
        return new ItemTemplate
        {
            Name = "临时钝器",
            Desc = "造成 {Damage} 伤害；减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Medium,
            Tags = [Tag.Weapon],
            Cooldown = 8.0,
            Damage = [20, 40, 80, 160],
            SlowTargetCount = 2,
            SlowSeconds = new[] { 3.0, 4.0, 5.0, 6.0 },
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Damage, Effect.Slow],
                },
            ],
        };
    }

    /// <summary>暗影斗篷（Shadowed Cloak）：中 铜 服饰。使用此物品右侧的物品时，使之加速 1 » 2 » 3 » 4 秒（优先级 Low）；若该物品为武器则再令伤害提高 +3 » +5 » +7 » +9（限本场战斗）。</summary>
    public static ItemTemplate ShadowedCloak()
    {
        return new ItemTemplate
        {
            Name = "暗影斗篷",
            Desc = "使用此物品右侧的物品时，使之加速 {HasteSeconds} 秒；若为武器则伤害提高 {Custom_0}（限本场战斗）",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Medium,
            Tags = [Tag.Apparel],
            HasteSeconds = new[] { 1.0, 2.0, 3.0, 4.0 },
            Custom_0 = [3, 5, 7, 9],
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseOtherItem,
                    Priority = AbilityPriority.Low,
                    Condition = Condition.UsedItemRightOfSource,
                    Effects = [Effect.Accelerate, Effect.WeaponDamageBonusToRightItem(nameof(ItemTemplate.Custom_0))],
                },
            ],
        };
    }

    /// <summary>注册所有公共中物品到数据库。</summary>
    public static void RegisterAll(ItemDatabase db)
    {
        db.Register(SpikedBuckler());
        db.Register(ImprovisedBludgeon());
        db.Register(ShadowedCloak());
    }
}
