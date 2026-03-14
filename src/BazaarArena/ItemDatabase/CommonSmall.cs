using BazaarArena.BattleSimulator;
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
            Desc = "▶ 造成 {Damage} 伤害",
            Tags = [Tag.Weapon],
            Cooldown = 3.0,
            Damage = [5, 10, 15, 20],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }

    /// <summary>岩浆核心：铜、小；每场战斗开始时造成 6 » 9 » 12 » 15 灼烧。</summary>
    public static ItemTemplate LavaCore()
    {
        return new ItemTemplate
        {
            Name = "岩浆核心",
            Desc = "▶ 战斗开始时，造成 {Burn} 灼烧",
            Tags = [],
            Burn = [6, 9, 12, 15],
            Abilities =
            [
                Ability.Burn.Override(
                    trigger: Trigger.BattleStart,
                    condition: Condition.Always
                ),
            ],
        };
    }

    /// <summary>驯化蜘蛛：铜、小；冷却 6 秒，造成 1 » 2 » 3 » 4 剧毒。标签：伙伴。</summary>
    public static ItemTemplate TrainedSpider()
    {
        return new ItemTemplate
        {
            Name = "驯化蜘蛛",
            Desc = "▶ 造成 {Poison} 剧毒",
            Tags = [Tag.Friend],
            Cooldown = 6.0,
            Poison = [1, 2, 3, 4],
            Abilities =
            [
                Ability.Poison,
            ],
        };
    }

    /// <summary>举重手套（Lifting Gloves）：小、铜；冷却 5 秒，武器伤害提高 1 » 2 » 3 » 4（限本场战斗），优先级 High。</summary>
    public static ItemTemplate LiftingGloves()
    {
        return new ItemTemplate
        {
            Name = "举重手套",
            Desc = "▶ 武器伤害提高 {Custom_0}（限本场战斗）",
            Tags = [Tag.Tool, Tag.Apparel],
            Cooldown = 5.0,
            Custom_0 = [1, 2, 3, 4],
            Abilities =
            [
                Ability.AddAttribute(Key.Damage).Override(
                    additionalTargetCondition: Condition.WithTag(Tag.Weapon),
                    priority: AbilityPriority.High
                ),
            ],
        };
    }

    /// <summary>符文手斧（Rune Axe）：8s 武器 小 铜，造成 15 » 30 » 60 » 120 伤害。</summary>
    public static ItemTemplate RuneAxe()
    {
        return new ItemTemplate
        {
            Name = "符文手斧",
            Desc = "▶ 造成 {Damage} 伤害",
            Tags = [Tag.Weapon],
            Cooldown = 8.0,
            Damage = [15, 30, 60, 120],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }

    /// <summary>放大镜（Magnifying Glass）：6s 武器 工具 小 铜，造成 5 » 15 » 30 » 50 伤害。</summary>
    public static ItemTemplate MagnifyingGlass()
    {
        return new ItemTemplate
        {
            Name = "放大镜",
            Desc = "▶ 造成 {Damage} 伤害",
            Tags = [Tag.Weapon, Tag.Tool],
            Cooldown = 6.0,
            Damage = [5, 15, 30, 50],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }

    /// <summary>古董剑（Old Sword）：5s 武器 小 铜，造成 5 » 10 » 20 » 40 伤害。</summary>
    public static ItemTemplate OldSword()
    {
        return new ItemTemplate
        {
            Name = "古董剑",
            Desc = "▶ 造成 {Damage} 伤害",
            Tags = [Tag.Weapon],
            Cooldown = 5.0,
            Damage = [5, 10, 20, 40],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }

    /// <summary>轻步靴（Agility Boots）：小、铜、服饰；相邻物品 +3% » +6% » +9% » +12% 暴击率（光环）。</summary>
    public static ItemTemplate AgilityBoots()
    {
        return new ItemTemplate
        {
            Name = "轻步靴",
            Desc = "▶ 相邻物品 {+Custom_0%} 暴击率",
            Tags = [Tag.Apparel],
            Custom_0 = [3, 6, 9, 12],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.CritRatePercent,
                    Condition = Condition.AdjacentToSource,
                    Value = Formula.Source(Key.Custom_0),
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
            Desc = "▶ 造成 {Damage} 伤害；此物品能造成双倍暴击伤害",
            Tags = [Tag.Weapon],
            Cooldown = 4.0,
            Damage = [10, 20, 30, 40],
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.CritDamagePercent,
                    Value = Formula.Constant(100),
                    Percent = true,
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
            Desc = "▶ 治疗 {Heal} 生命值",
            Tags = [Tag.Food],
            Cooldown = 10.0,
            Heal = [10, 20, 40, 80],
            Abilities =
            [
                Ability.Heal,
            ],
        };
    }

    /// <summary>冰锥（Icicle）：小、铜；每场战斗开始时，冻结一件物品 3 » 4 » 5 » 6 秒。有冷却的敌人物品优先被选取。</summary>
    public static ItemTemplate Icicle()
    {
        return new ItemTemplate
        {
            Name = "冰锥",
            Desc = "▶ 战斗开始时，冻结 {FreezeTargetCount} 件物品 {FreezeSeconds} 秒",
            Tags = [],
            Freeze = [3.0, 4.0, 5.0, 6.0],
            Abilities =
            [
                Ability.Freeze.Override(
                    trigger: Trigger.BattleStart,
                    condition: Condition.Always
                ),
            ],
        };
    }

    /// <summary>毒刺（Stinger）：7s 小 铜 武器，造成 5 » 10 » 20 » 40 伤害；减速 1 » 2 » 3 » 4 件物品 1 秒；吸血。</summary>
    public static ItemTemplate Stinger()
    {
        return new ItemTemplate
        {
            Name = "毒刺",
            Desc = "▶ 造成 {Damage} 伤害；减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；吸血",
            Tags = [Tag.Weapon],
            Cooldown = 7.0,
            Damage = [5, 10, 20, 40],
            LifeSteal = 1,
            Slow = 1.0,
            SlowTargetCount = [1, 2, 3, 4],
            Abilities =
            [
                Ability.Damage,
                Ability.Slow,
            ],
        };
    }

    /// <summary>裂盾刀（Sunderer）：5s 小 铜 武器，造成 10 » 20 » 30 » 40 伤害；敌人的护盾物品损失 5 » 10 » 15 » 20 护盾（限本场战斗）（优先级 High）。</summary>
    public static ItemTemplate Sunderer()
    {
        return new ItemTemplate
        {
            Name = "裂盾刀",
            Desc = "▶ 造成 {Damage} 伤害；敌人的护盾物品损失 {Custom_0} 护盾（限本场战斗）",
            Tags = [Tag.Weapon],
            Cooldown = 5.0,
            Damage = [10, 20, 30, 40],
            Custom_0 = [5, 10, 15, 20],
            Abilities =
            [
                Ability.Damage,
                Ability.ReduceAttribute(Key.Shield).Override(
                    additionalTargetCondition: Condition.WithTag(Tag.Shield),
                    priority: AbilityPriority.High
                ),
            ],
        };
    }

    /// <summary>灵质（Ectoplasm）：7s 银 小；造成 10 » 20 » 30 剧毒；获得治疗，等量于敌人的剧毒（Heal=0，光环将敌人剧毒加到 Heal）。</summary>
    public static ItemTemplate Ectoplasm()
    {
        return new ItemTemplate
        {
            Name = "灵质",
            Desc = "▶ 造成 {Poison} 剧毒；获得治疗，等量于敌人的剧毒",
            Tags = [],
            Cooldown = 7.0,
            Poison = [10, 20, 30],
            Heal = 0,
            Abilities =
            [
                Ability.Poison,
                Ability.Heal,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.Heal,
                    Value = Formula.Opp(BattleSide.KeyPoison),
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
            Desc = "▶ 造成 {Poison} 剧毒；相邻物品触发减速时，此物品获得 {Custom_0} 剧毒（限本场战斗）",
            Tags = [Tag.Relic],
            Cooldown = 6.0,
            Poison = 4,
            Custom_0 = [4, 8, 12],
            Abilities =
            [
                Ability.Poison,
                Ability.AddAttribute(Key.Poison).Override(
                    trigger: Trigger.Slow,
                    condition: Condition.AdjacentToSource,
                    targetCondition: Condition.SameAsSource,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }

    /// <summary>神经毒素（Neural Toxin）：银 小；使用相邻武器时，减速 1 件物品 1 » 2 » 3 秒。</summary>
    public static ItemTemplate NeuralToxin()
    {
        return new ItemTemplate
        {
            Name = "神经毒素",
            Desc = "▶ 使用相邻武器时，减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒",
            Tags = [],
            Slow = [1.0, 2.0, 3.0],
            Abilities =
            [
                Ability.Slow.Override(
                    condition: Condition.AdjacentToSource & Condition.WithTag(Tag.Weapon)
                ),
            ],
        };
    }

    /// <summary>断裂镣铐（Broken Shackles）：8s 小 银；武器伤害提高 4 » 8 » 12（限本场战斗）（优先级 High）；使用武器时为此物品充能 2 秒。</summary>
    public static ItemTemplate BrokenShackles()
    {
        return new ItemTemplate
        {
            Name = "断裂镣铐",
            Desc = "▶ 武器伤害提高 {Custom_0}（限本场战斗）；使用武器时，为此物品充能 {ChargeSeconds} 秒",
            Tags = [],
            Cooldown = 8.0,
            Custom_0 = [4, 8, 12],
            Charge = 2.0,
            Abilities =
            [
                Ability.AddAttribute(Key.Damage).Override(
                    additionalTargetCondition: Condition.WithTag(Tag.Weapon),
                    priority: AbilityPriority.High
                ),
                Ability.Charge.Override(
                    condition: Condition.SameSide & Condition.WithTag(Tag.Weapon),
                    targetCondition: Condition.SameAsSource
                ),
            ],
        };
    }

    /// <summary>宇宙护符（Cosmic Amulet）：5s 小 银 遗物；加速一件物品 1 » 2 » 3 秒；造成暴击时此物品开始飞行（优先级 Low）；此物品飞行时 +1 多重释放。</summary>
    public static ItemTemplate CosmicAmulet()
    {
        return new ItemTemplate
        {
            Name = "宇宙护符",
            Desc = "▶ 加速 {HasteTargetCount} 件物品 {HasteSeconds} 秒；造成暴击时，此物品开始飞行；此物品飞行时，{+Custom_0} 多重释放",
            Tags = [Tag.Relic],
            Cooldown = 5.0,
            Haste = [1.0, 2.0, 3.0],
            Custom_0 = 1,
            Abilities =
            [
                Ability.Haste,
                Ability.StartFlying.Override(
                    trigger: Trigger.Crit,
                    condition: Condition.SameSide,
                    targetCondition: Condition.SameAsSource,
                    priority: AbilityPriority.Low
                ),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.Multicast,
                    SourceCondition = Condition.InFlight,
                    Value = Formula.Source(Key.Custom_0),
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
            Desc = "▶ 造成 {Damage} 伤害；造成灼烧，等量于此物品伤害；此物品开始飞行",
            Tags = [Tag.Weapon, Tag.Friend, Tag.Dragon],
            Cooldown = [9.0, 8.0, 7.0],
            Damage = 5,
            Burn = 0,
            Abilities =
            [
                Ability.Damage,
                Ability.Burn,
                Ability.StartFlying.Override(targetCondition: Condition.SameAsSource),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.Burn,
                    Value = Formula.Source(Key.Damage),
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
            Desc = "▶ 获得 {Shield} 护盾；使用工具时，为此物品充能 {ChargeSeconds} 秒",
            Tags = [Tag.Food, Tag.Friend],
            Cooldown = 5.0,
            Shield = [10, 20, 30, 40],
            Charge = 1.0,
            Abilities =
            [
                Ability.Shield.Override(
                    priority: AbilityPriority.Low
                ),
                Ability.Charge.Override(
                    condition: Condition.SameSide & Condition.WithTag(Tag.Tool),
                    targetCondition: Condition.SameAsSource
                ),
            ],
        };
    }

    /// <summary>纳米机器人（Nanobot）：6s 小 银 武器 伙伴；每拥有一位伙伴造成 15 » 20 » 25 伤害；每有一个相邻的伙伴，冷却时间缩短 1 秒。</summary>
    public static ItemTemplate Nanobot()
    {
        return new ItemTemplate
        {
            Name = "纳米机器人",
            Desc = "▶ 每拥有一位伙伴，造成 {Custom_0} 伤害；每有一个相邻的伙伴，冷却时间缩短 1 秒",
            Tags = [Tag.Weapon, Tag.Friend],
            Cooldown = 6.0,
            Custom_0 = [15, 20, 25],
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.Damage,
                    Value = Formula.Source(Key.Custom_0) * Formula.Count(Condition.SameSide & Condition.WithTag(Tag.Friend)),
                },
                new AuraDefinition
                {
                    AttributeName = Key.CooldownMs,
                    Value = Formula.Constant(-1000) * Formula.Count(Condition.AdjacentToSource & Condition.WithTag(Tag.Friend)),
                },
            ],
        };
    }

    /// <summary>工蜂（Busy Bee）：6s 小 银 武器 伙伴 无人机；造成 5 » 10 » 20 伤害。</summary>
    public static ItemTemplate BusyBee()
    {
        return new ItemTemplate
        {
            Name = "工蜂",
            Desc = "▶ 造成 {Damage} 伤害",
            Tags = [Tag.Weapon, Tag.Friend, Tag.Drone],
            Cooldown = 6.0,
            Damage = [5, 10, 20],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }

    /// <summary>口器（Proboscis）：小 银 武器；触发减速时造成 8 » 12 » 16 伤害（优先级 Low）。</summary>
    public static ItemTemplate Proboscis()
    {
        return new ItemTemplate
        {
            Name = "口器",
            Desc = "▶ 触发减速时，造成 {Damage} 伤害",
            Tags = [Tag.Weapon],
            Damage = [8, 12, 16],
            Abilities =
            [
                Ability.Damage.Override(
                    trigger: Trigger.Slow,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }

    /// <summary>友好玩偶（Friendly Doll）：3s 小 银 武器 伙伴 玩具；造成 5 » 15 » 25 伤害；若此为唯一伙伴，暴击率 +50% » +75% » +100%（可超过 100%）。</summary>
    public static ItemTemplate FriendlyDoll()
    {
        return new ItemTemplate
        {
            Name = "友好玩偶",
            Desc = "▶ 造成 {Damage} 伤害；若此为唯一伙伴，暴击率 {+Custom_0%}",
            Tags = [Tag.Weapon, Tag.Friend, Tag.Toy],
            Cooldown = 3.0,
            Damage = [5, 15, 25],
            Custom_0 = [50, 75, 100],
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.CritRatePercent,
                    SourceCondition = Condition.OnlyCompanion,
                    Value = Formula.Source(Key.Custom_0),
                },
            ],
        };
    }

    /// <summary>牵引光束（Tractor Beam）：6s 小 银 武器；使用物品时摧毁右侧下一件己方物品，造成 150 » 300 » 600 伤害；若被毁物品为大型或飞行，再造成等量伤害。</summary>
    public static ItemTemplate TractorBeam()
    {
        return new ItemTemplate
        {
            Name = "牵引光束",
            Desc = "▶ 摧毁右侧下一件己方物品，造成 {Damage} 伤害；若被毁物品为大型或飞行，再造成 {Damage} 伤害",
            Tags = [Tag.Weapon],
            Cooldown = 6.0,
            Damage = [150, 300, 600],
            Abilities =
            [
                Ability.Destroy.Override(
                    additionalTargetCondition: Condition.FirstNonDestroyedRightOfSource
                ),
                Ability.Damage.Override(
                    trigger: Trigger.Destroy,
                    condition: Condition.SameAsSource
                ),
                Ability.Damage.Override(
                    trigger: Trigger.Destroy,
                    condition: Condition.SameAsSource,
                    invokeTargetCondition: Condition.WithTag(Tag.Large) | Condition.InFlight
                ),
            ],
        };
    }

    /// <summary>注册所有公共小物品到数据库。先注册所有铜物品，再注册所有银物品。</summary>
    public static void RegisterAll(ItemDatabase db)
    {
        db.DefaultSize = ItemSize.Small;

        db.DefaultMinTier = ItemTier.Bronze;
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
        db.Register(GingerbreadMan());

        db.DefaultMinTier = ItemTier.Silver;
        db.Register(Ectoplasm());
        db.Register(ForgottenGod());
        db.Register(NeuralToxin());
        db.Register(BrokenShackles());
        db.Register(CosmicAmulet());
        db.Register(DragonWhelp());
        db.Register(Nanobot());
        db.Register(BusyBee());
        db.Register(Proboscis());
        db.Register(FriendlyDoll());
        db.Register(TractorBeam());
    }
}
