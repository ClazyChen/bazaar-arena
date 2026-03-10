using BazaarArena.Core;

namespace BazaarArena.ItemDatabase;

/// <summary>公共小物品模板：供卡组与模拟共用的常见小型物品。</summary>
public static class CommonSmall
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
            Tags = [Tag.Weapon],
            Cooldown = 3.0,
            Damage = [5, 10, 15, 20],
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
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
                    TriggerName = Trigger.BattleStart,
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
            Tags = [Tag.Friend],
            Cooldown = 6.0,
            Poison = [1, 2, 3, 4],
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
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
            Tags = [Tag.Tool, Tag.Apparel],
            Cooldown = 5.0,
            Custom_0 = [1, 2, 3, 4],
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.High,
                    Effects = [Effect.AddAttribute(nameof(ItemTemplate.Damage), targetCondition: Condition.WithTag(Tag.Weapon))],
                },
            ],
        };
    }

    /// <summary>符文手斧（Rune Axe）：8s 武器 小 铜，造成 15 » 30 » 60 » 120 伤害。</summary>
    public static ItemTemplate RuneAxe()
    {
        return new ItemTemplate
        {
            Name = "符文手斧",
            Desc = "造成 {Damage} 伤害",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = [Tag.Weapon],
            Cooldown = 8.0,
            Damage = [15, 30, 60, 120],
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Damage],
                },
            ],
        };
    }

    /// <summary>放大镜（Magnifying Glass）：6s 武器 工具 小 铜，造成 5 » 15 » 30 » 50 伤害。</summary>
    public static ItemTemplate MagnifyingGlass()
    {
        return new ItemTemplate
        {
            Name = "放大镜",
            Desc = "造成 {Damage} 伤害",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = [Tag.Weapon, Tag.Tool],
            Cooldown = 6.0,
            Damage = [5, 15, 30, 50],
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Damage],
                },
            ],
        };
    }

    /// <summary>古董剑（Old Sword）：5s 武器 小 铜，造成 5 » 10 » 20 » 40 伤害。</summary>
    public static ItemTemplate OldSword()
    {
        return new ItemTemplate
        {
            Name = "古董剑",
            Desc = "造成 {Damage} 伤害",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = [Tag.Weapon],
            Cooldown = 5.0,
            Damage = [5, 10, 20, 40],
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Damage],
                },
            ],
        };
    }

    /// <summary>轻步靴（Agility Boots）：小、铜、服饰；相邻物品 +3% » +6% » +9% » +12% 暴击率（光环）。</summary>
    public static ItemTemplate AgilityBoots()
    {
        return new ItemTemplate
        {
            Name = "轻步靴",
            Desc = "相邻物品 {+Custom_0%} 暴击率",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = [Tag.Apparel],
            Custom_0 = [3, 6, 9, 12],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = nameof(ItemTemplate.CritRatePercent),
                    Condition = Condition.AdjacentToSource,
                    FixedValueKey = nameof(ItemTemplate.Custom_0),
                },
            ],
        };
    }

    /// <summary>利爪（Claws）：4s 小 铜 武器，造成 10 » 20 » 30 » 40 伤害；此物品能造成双倍暴击伤害（光环：自身暴击伤害 +100%）。</summary>
    public static ItemTemplate Claws()
    {
        return new ItemTemplate
        {
            Name = "利爪",
            Desc = "造成 {Damage} 伤害；此物品能造成双倍暴击伤害",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = [Tag.Weapon],
            Cooldown = 4.0,
            Damage = [10, 20, 30, 40],
            Custom_0 = 100,
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Damage],
                },
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = nameof(ItemTemplate.CritDamagePercent),
                    Condition = Condition.SameAsSource,
                    PercentValueKey = nameof(ItemTemplate.Custom_0),
                },
            ],
        };
    }

    /// <summary>蓝蕉（Bluenanas）：10s 食物 小 铜，治疗 10 » 20 » 40 » 80 生命值。</summary>
    public static ItemTemplate Bluenanas()
    {
        return new ItemTemplate
        {
            Name = "蓝蕉",
            Desc = "治疗 {Heal} 生命值",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = [Tag.Food],
            Cooldown = 10.0,
            Heal = [10, 20, 40, 80],
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Heal],
                },
            ],
        };
    }

    /// <summary>冰锥（Icicle）：小、铜；每场战斗开始时，冻结一件物品 3 » 4 » 5 » 6 秒。有冷却的敌人物品优先被选取。</summary>
    public static ItemTemplate Icicle()
    {
        return new ItemTemplate
        {
            Name = "冰锥",
            Desc = "每场战斗开始时，冻结一件物品 {FreezeSeconds} 秒",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = [],
            FreezeSeconds = new[] { 3.0, 4.0, 5.0, 6.0 },
            FreezeTargetCount = 1,
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.BattleStart,
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Freeze],
                },
            ],
        };
    }

    /// <summary>毒刺（Stinger）：7s 小 铜 武器，造成 5 » 10 » 20 » 40 伤害；减速 1 » 2 » 3 » 4 件物品 1 秒；吸血。</summary>
    public static ItemTemplate Stinger()
    {
        return new ItemTemplate
        {
            Name = "毒刺",
            Desc = "造成 {Damage} 伤害；减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；吸血",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = [Tag.Weapon],
            Cooldown = 7.0,
            Damage = [5, 10, 20, 40],
            LifeSteal = 1,
            SlowSeconds = 1.0,
            SlowTargetCount = [1, 2, 3, 4],
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

    /// <summary>裂盾刀（Sunderer）：5s 小 铜 武器，造成 10 » 20 » 30 » 40 伤害；敌人的护盾物品损失 5 » 10 » 15 » 20 护盾（限本场战斗）（优先级 High）。</summary>
    public static ItemTemplate Sunderer()
    {
        return new ItemTemplate
        {
            Name = "裂盾刀",
            Desc = "造成 {Damage} 伤害；敌人的护盾物品损失 {Custom_0} 护盾（限本场战斗）",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = [Tag.Weapon],
            Cooldown = 5.0,
            Damage = [10, 20, 30, 40],
            Custom_0 = [5, 10, 15, 20],
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Damage],
                },
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.High,
                    Effects = [Effect.ReduceAttribute(nameof(ItemTemplate.Shield), targetCondition: Condition.IsShieldItem)],
                },
            ],
        };
    }

    /// <summary>灵质（Ectoplasm）：7s 银 小；造成 10 » 20 » 30 剧毒；获得治疗，等量于敌人的剧毒（Heal=0，光环将敌人剧毒加到 Heal）。</summary>
    public static ItemTemplate Ectoplasm()
    {
        return new ItemTemplate
        {
            Name = "灵质",
            Desc = "造成 {Poison} 剧毒；获得治疗，等量于敌人的剧毒",
            MinTier = ItemTier.Silver,
            Size = ItemSize.Small,
            Tags = [],
            Cooldown = 7.0,
            Poison = [10, 20, 30],
            Heal = 0,
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Poison, Effect.Heal],
                },
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = nameof(ItemTemplate.Heal),
                    Condition = Condition.SameAsSource,
                    FixedValueFormula = Formula.OpponentPoison,
                },
            ],
        };
    }

    /// <summary>失落神祇（Forgotten God）：6s 银 小 遗物；造成 4 剧毒；相邻物品触发减速时，此物品获得 4 » 8 » 12 剧毒（限本场战斗）（优先级 Low）。</summary>
    public static ItemTemplate ForgottenGod()
    {
        return new ItemTemplate
        {
            Name = "失落神祇",
            Desc = "造成 {Poison} 剧毒；相邻物品触发减速时，此物品获得 {Custom_0} 剧毒（限本场战斗）",
            MinTier = ItemTier.Silver,
            Size = ItemSize.Small,
            Tags = [Tag.Relic],
            Cooldown = 6.0,
            Poison = 4,
            Custom_0 = [4, 8, 12],
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Poison],
                },
                new()
                {
                    TriggerName = Trigger.Slow,
                    Priority = AbilityPriority.Low,
                    Condition = Condition.AdjacentToSource,
                    Effects = [Effect.AddAttribute(nameof(ItemTemplate.Poison))],
                },
            ],
        };
    }

    /// <summary>神经毒素（Neural Toxin）：银 小；使用相邻武器时，减速 1 件物品 1 » 2 » 3 秒。</summary>
    public static ItemTemplate NeuralToxin()
    {
        return new ItemTemplate
        {
            Name = "神经毒素",
            Desc = "使用相邻武器时，减速 1 件物品 {SlowSeconds} 秒",
            MinTier = ItemTier.Silver,
            Size = ItemSize.Small,
            Tags = [],
            SlowSeconds = new[] { 1.0, 2.0, 3.0 },
            SlowTargetCount = 1,
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseOtherItem,
                    Priority = AbilityPriority.Medium,
                    Condition = Condition.And(Condition.AdjacentToSource, Condition.WithTag(Tag.Weapon)),
                    Effects = [Effect.Slow],
                },
            ],
        };
    }

    /// <summary>断裂镣铐（Broken Shackles）：8s 小 银；武器伤害提高 4 » 8 » 12（限本场战斗）（优先级 High）；使用武器时为此物品充能 2 秒。</summary>
    public static ItemTemplate BrokenShackles()
    {
        return new ItemTemplate
        {
            Name = "断裂镣铐",
            Desc = "武器伤害提高 {Custom_0}（限本场战斗）；使用武器时，为此物品充能 {ChargeSeconds} 秒",
            MinTier = ItemTier.Silver,
            Size = ItemSize.Small,
            Tags = [],
            Cooldown = 8.0,
            Custom_0 = [4, 8, 12],
            ChargeSeconds = 2.0,
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.High,
                    Effects = [Effect.AddAttribute(nameof(ItemTemplate.Damage), targetCondition: Condition.WithTag(Tag.Weapon))],
                },
                new()
                {
                    TriggerName = Trigger.UseOtherItem,
                    Priority = AbilityPriority.Medium,
                    Condition = Condition.WithTag(Tag.Weapon),
                    Effects = [Effect.ChargeSelf],
                },
            ],
        };
    }

    /// <summary>宇宙护符（Cosmic Amulet）：5s 小 银 遗物；加速一件物品 1 » 2 » 3 秒；造成暴击时此物品开始飞行（优先级 Low）；此物品飞行时 +1 多重释放。</summary>
    public static ItemTemplate CosmicAmulet()
    {
        return new ItemTemplate
        {
            Name = "宇宙护符",
            Desc = "加速一件物品 {HasteSeconds} 秒；造成暴击时，此物品开始飞行；此物品飞行时，+1 多重释放",
            MinTier = ItemTier.Silver,
            Size = ItemSize.Small,
            Tags = [Tag.Relic],
            Cooldown = 5.0,
            HasteSeconds = new[] { 1.0, 2.0, 3.0 },
            HasteTargetCount = 1,
            Custom_0 = 1,
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Haste],
                },
                new()
                {
                    TriggerName = Trigger.OnCrit,
                    Priority = AbilityPriority.Low,
                    Effects = [Effect.StartFlying],
                },
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = nameof(ItemTemplate.Multicast),
                    Condition = Condition.And(Condition.SameAsSource, Condition.InFlight),
                    FixedValueKey = nameof(ItemTemplate.Custom_0),
                },
            ],
        };
    }

    /// <summary>巨龙崽崽（Dragon Whelp）：9 » 8 » 7s 小 银 武器 伙伴 巨龙；造成 5 伤害；造成灼烧等量于此物品伤害；此物品开始飞行。</summary>
    public static ItemTemplate DragonWhelp()
    {
        return new ItemTemplate
        {
            Name = "巨龙崽崽",
            Desc = "造成 {Damage} 伤害；造成灼烧，等量于此物品伤害；此物品开始飞行",
            MinTier = ItemTier.Silver,
            Size = ItemSize.Small,
            Tags = [Tag.Weapon, Tag.Friend, Tag.Dragon],
            CooldownMs = [9000, 8000, 7000],
            Damage = 5,
            Burn = 0,
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Medium,
                    Effects = [Effect.Damage, Effect.Burn, Effect.StartFlying],
                },
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = nameof(ItemTemplate.Burn),
                    Condition = Condition.SameAsSource,
                    FixedValueFormula = Formula.SourceDamage,
                },
            ],
        };
    }

    /// <summary>姜饼人（Gingerbread Man）：5s 小 铜 食物 伙伴；获得 10 » 20 » 30 » 40 护盾（优先级 Low）；使用工具时为此物品充能 1 秒（优先级 Medium）。</summary>
    public static ItemTemplate GingerbreadMan()
    {
        return new ItemTemplate
        {
            Name = "姜饼人",
            Desc = "获得 {Shield} 护盾；使用工具时，为此物品充能 {ChargeSeconds} 秒",
            MinTier = ItemTier.Bronze,
            Size = ItemSize.Small,
            Tags = [Tag.Food, Tag.Friend],
            Cooldown = 5.0,
            Shield = [10, 20, 30, 40],
            ChargeSeconds = 1.0,
            Abilities =
            [
                new()
                {
                    TriggerName = Trigger.UseItem,
                    Priority = AbilityPriority.Low,
                    Effects = [Effect.Shield],
                },
                new()
                {
                    TriggerName = Trigger.UseOtherItem,
                    Priority = AbilityPriority.Medium,
                    Condition = Condition.WithTag(Tag.Tool),
                    Effects = [Effect.ChargeSelf],
                },
            ],
        };
    }

    /// <summary>注册所有公共小物品到数据库。</summary>
    public static void RegisterAll(ItemDatabase db)
    {
        db.Register(Fang());
        db.Register(LavaCore());
        db.Register(TrainedSpider());
        db.Register(LiftingGloves());
        db.Register(RuneAxe());
        db.Register(MagnifyingGlass());
        db.Register(OldSword());
        db.Register(AgilityBoots());
        db.Register(Claws());
        db.Register(Bluenanas());
        db.Register(Icicle());
        db.Register(Stinger());
        db.Register(Sunderer());
        db.Register(Ectoplasm());
        db.Register(ForgottenGod());
        db.Register(NeuralToxin());
        db.Register(GingerbreadMan());
        db.Register(BrokenShackles());
        db.Register(CosmicAmulet());
        db.Register(DragonWhelp());
    }
}
