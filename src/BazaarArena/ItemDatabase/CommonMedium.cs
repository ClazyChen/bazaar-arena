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
            Tags = [Tag.Weapon],
            Cooldown = 9.0,
            Damage = [10, 20, 40, 80],
            Shield = [10, 20, 40, 80],
            Abilities =
            [
                Ability.Damage(),
                Ability.Shield(),
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
            Tags = [Tag.Weapon],
            Cooldown = 8.0,
            Damage = [20, 40, 80, 160],
            SlowTargetCount = 2,
            SlowSeconds = new[] { 3.0, 4.0, 5.0, 6.0 },
            Abilities =
            [
                Ability.Damage(),
                Ability.Slow(),
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
            Tags = [Tag.Apparel],
            HasteSeconds = new[] { 1.0, 2.0, 3.0, 4.0 },
            HasteTargetCount = 1,
            Custom_0 = [3, 5, 7, 9],
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Low,
                    Condition = Condition.DifferentFromSource & Condition.SameSide & Condition.RightOfSource,
                    TargetCondition = Condition.RightOfSource,
                    ApplyCritMultiplier = false,
                    Apply = Effect.HasteApply,
                },
                Ability.AddAttribute(nameof(ItemTemplate.Damage), additionalTargetCondition: Condition.RightOfSource & Condition.WithTag(Tag.Weapon), priority: AbilityPriority.Low, condition: Condition.DifferentFromSource & Condition.SameSide & Condition.RightOfSource),
            ],
        };
    }

    /// <summary>冰冻钝器（Frozen Bludgeon）：9s 中 铜 武器，造成 20 » 40 » 60 » 80 伤害；冻结 1 » 2 » 3 » 4 件物品 1 秒；触发冻结时，己方武器伤害提高 5 » 10 » 15 » 20（限本场战斗）。</summary>
    public static ItemTemplate FrozenBludgeon()
    {
        return new ItemTemplate
        {
            Name = "冰冻钝器",
            Desc = "造成 {Damage} 伤害；冻结 {FreezeTargetCount} 件物品 {FreezeSeconds} 秒；触发冻结时，己方武器伤害提高 {Custom_0}（限本场战斗）",
            Tags = [Tag.Weapon],
            Cooldown = 9.0,
            Damage = [20, 40, 60, 80],
            FreezeSeconds = 1.0,
            FreezeTargetCount = [1, 2, 3, 4],
            Custom_0 = [5, 10, 15, 20],
            Abilities =
            [
                Ability.Damage(),
                Ability.Freeze(),
                Ability.AddAttribute(nameof(ItemTemplate.Damage), additionalTargetCondition: Condition.WithTag(Tag.Weapon), trigger: Trigger.Freeze, priority: AbilityPriority.Low),
            ],
        };
    }

    /// <summary>发条刀（Clockwork Blades）：4s 中 铜 武器，造成 20 » 40 » 80 » 160 伤害。</summary>
    public static ItemTemplate ClockworkBlades()
    {
        return new ItemTemplate
        {
            Name = "发条刀",
            Desc = "造成 {Damage} 伤害",
            Tags = [Tag.Weapon],
            Cooldown = 4.0,
            Damage = [20, 40, 80, 160],
            Abilities =
            [
                Ability.Damage(),
            ],
        };
    }

    /// <summary>大理石鳞甲（Marble Scalemail）：9s 中 铜 服饰，获得 20 » 60 » 120 » 200 护盾。</summary>
    public static ItemTemplate MarbleScalemail()
    {
        return new ItemTemplate
        {
            Name = "大理石鳞甲",
            Desc = "获得 {Shield} 护盾",
            Tags = [Tag.Apparel],
            Cooldown = 9.0,
            Shield = [20, 60, 120, 200],
            Abilities =
            [
                Ability.Shield(),
            ],
        };
    }

    /// <summary>废品场大棒（Junkyard Club）：11s 中 铜 武器，造成 30 » 60 » 120 » 240 伤害。</summary>
    public static ItemTemplate JunkyardClub()
    {
        return new ItemTemplate
        {
            Name = "废品场大棒",
            Desc = "造成 {Damage} 伤害",
            Tags = [Tag.Weapon],
            Cooldown = 11.0,
            Damage = [30, 60, 120, 240],
            Abilities =
            [
                Ability.Damage(),
            ],
        };
    }

    /// <summary>火箭靴（Rocket Boots）：5s 中 铜 工具 服饰，加速相邻物品 1 » 2 » 3 » 4 秒（优先级 High）。</summary>
    public static ItemTemplate RocketBoots()
    {
        return new ItemTemplate
        {
            Name = "火箭靴",
            Desc = "加速相邻物品 {HasteSeconds} 秒",
            Tags = [Tag.Tool, Tag.Apparel],
            Cooldown = 5.0,
            HasteSeconds = new[] { 1.0, 2.0, 3.0, 4.0 },
            HasteTargetCount = 2,
            Abilities =
            [
                Ability.Haste(priority: AbilityPriority.High, targetCondition: Condition.AdjacentToSource),
            ],
        };
    }

    /// <summary>火蜥幼兽（Salamander Pup）：8s 中 铜 伙伴，造成 4 » 6 » 8 » 10 灼烧。</summary>
    public static ItemTemplate SalamanderPup()
    {
        return new ItemTemplate
        {
            Name = "火蜥幼兽",
            Desc = "造成 {Burn} 灼烧",
            Tags = [Tag.Friend],
            Cooldown = 8.0,
            Burn = [4, 6, 8, 10],
            Abilities =
            [
                Ability.Burn(),
            ],
        };
    }

    /// <summary>简易路障（Makeshift Barricade）：7s 中 铜，减速 1 件物品 1 » 2 » 3 » 4 秒。</summary>
    public static ItemTemplate MakeshiftBarricade()
    {
        return new ItemTemplate
        {
            Name = "简易路障",
            Desc = "减速 1 件物品 {SlowSeconds} 秒",
            Tags = [],
            Cooldown = 7.0,
            SlowSeconds = new[] { 1.0, 2.0, 3.0, 4.0 },
            SlowTargetCount = 1,
            Abilities =
            [
                Ability.Slow(),
            ],
        };
    }

    /// <summary>外骨骼（Exoskeleton）：中 铜 服饰，相邻武器 +5 » +10 » +20 » +40 伤害。</summary>
    public static ItemTemplate Exoskeleton()
    {
        return new ItemTemplate
        {
            Name = "外骨骼",
            Desc = "相邻武器 {+Custom_0} 伤害",
            Tags = [Tag.Apparel],
            Custom_0 = [5, 10, 20, 40],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = nameof(ItemTemplate.Damage),
                    Condition = Condition.AdjacentToSource & Condition.WithTag(Tag.Weapon),
                    FixedValueKey = nameof(ItemTemplate.Custom_0),
                },
            ],
        };
    }

    /// <summary>废品场维修机器人（Junkyard Repairbot）：5s 中 铜 伙伴 科技，修复 1 件物品（优先级 Lowest）；治疗 30 » 60 » 120 » 240 生命值。</summary>
    public static ItemTemplate JunkyardRepairbot()
    {
        return new ItemTemplate
        {
            Name = "废品场维修机器人",
            Desc = "修复 {RepairTargetCount} 件物品；治疗 {Heal} 生命值",
            Tags = [Tag.Friend, Tag.Tech],
            Cooldown = 5.0,
            RepairTargetCount = 1,
            Heal = [30, 60, 120, 240],
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Lowest,
                    ApplyCritMultiplier = false,
                    Apply = Effect.RepairApply,
                },
                Ability.Heal(),
            ],
        };
    }

    /// <summary>注册所有公共中物品到数据库。</summary>
    public static void RegisterAll(ItemDatabase db)
    {
        db.DefaultSize = ItemSize.Medium;
        db.DefaultMinTier = ItemTier.Bronze;
        db.Register(SpikedBuckler());
        db.Register(ImprovisedBludgeon());
        db.Register(ShadowedCloak());
        db.Register(FrozenBludgeon());
        db.Register(ClockworkBlades());
        db.Register(MarbleScalemail());
        db.Register(JunkyardClub());
        db.Register(RocketBoots());
        db.Register(SalamanderPup());
        db.Register(MakeshiftBarricade());
        db.Register(Exoskeleton());
        db.Register(JunkyardRepairbot());
    }
}
