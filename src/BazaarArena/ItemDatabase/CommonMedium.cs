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
            Desc = "▶ 造成 {Damage} 伤害；获得 {Shield} 护盾",
            Tags = [Tag.Weapon],
            Cooldown = 9.0,
            Damage = [10, 20, 40, 80],
            Shield = [10, 20, 40, 80],
            Abilities =
            [
                Ability.Damage,
                Ability.Shield,
            ],
        };
    }

    /// <summary>临时钝器（Improvised Bludgeon）：8s 中 铜 武器，造成 20 » 40 » 80 » 160 伤害；减速 2 件物品 3 » 4 » 5 » 6 秒。</summary>
    public static ItemTemplate ImprovisedBludgeon()
    {
        return new ItemTemplate
        {
            Name = "临时钝器",
            Desc = "▶ 造成 {Damage} 伤害；减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒",
            Tags = [Tag.Weapon],
            Cooldown = 8.0,
            Damage = [20, 40, 80, 160],
            SlowTargetCount = 2,
            Slow = [3.0, 4.0, 5.0, 6.0],
            Abilities =
            [
                Ability.Damage,
                Ability.Slow,
            ],
        };
    }

    /// <summary>暗影斗篷（Shadowed Cloak）：中 铜 服饰。使用此物品右侧的物品时，使之加速 1 » 2 » 3 » 4 秒（优先级 Low）；若该物品为武器则再令伤害提高 +3 » +5 » +7 » +9（限本场战斗）。</summary>
    public static ItemTemplate ShadowedCloak()
    {
        return new ItemTemplate
        {
            Name = "暗影斗篷",
            Desc = "▶ 使用此物品右侧的物品时，使之加速 {HasteSeconds} 秒；若为武器则伤害提高 {Custom_0}（限本场战斗）",
            Tags = [Tag.Apparel],
            Haste = [1.0, 2.0, 3.0, 4.0],
            Custom_0 = [3, 5, 7, 9],
            Abilities =
            [
                Ability.Haste.Override(
                    condition: Condition.RightOfSource,
                    targetCondition: Condition.RightOfSource,
                    priority: AbilityPriority.Low
                ),
                Ability.AddAttribute(Key.Damage).Override(
                    condition: Condition.RightOfSource & Condition.WithTag(Tag.Weapon),
                    targetCondition: Condition.RightOfSource,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }

    /// <summary>冰冻钝器（Frozen Bludgeon）：9s 中 铜 武器，造成 20 » 40 » 60 » 80 伤害；冻结 1 » 2 » 3 » 4 件物品 1 秒；触发冻结时，己方武器伤害提高 5 » 10 » 15 » 20（限本场战斗）。</summary>
    public static ItemTemplate FrozenBludgeon()
    {
        return new ItemTemplate
        {
            Name = "冰冻钝器",
            Desc = "▶ 造成 {Damage} 伤害；冻结 {FreezeTargetCount} 件物品 {FreezeSeconds} 秒；触发冻结时，己方武器伤害提高 {Custom_0}（限本场战斗）",
            Tags = [Tag.Weapon],
            Cooldown = 9.0,
            Damage = [20, 40, 60, 80],
            Freeze = 1.0,
            FreezeTargetCount = [1, 2, 3, 4],
            Custom_0 = [5, 10, 15, 20],
            Abilities =
            [
                Ability.Damage,
                Ability.Freeze,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Freeze,
                    additionalTargetCondition: Condition.WithTag(Tag.Weapon),
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }

    /// <summary>发条刀（Clockwork Blades）：4s 中 铜 武器，造成 20 » 40 » 80 » 160 伤害。</summary>
    public static ItemTemplate ClockworkBlades()
    {
        return new ItemTemplate
        {
            Name = "发条刀",
            Desc = "▶ 造成 {Damage} 伤害",
            Tags = [Tag.Weapon],
            Cooldown = 4.0,
            Damage = [20, 40, 80, 160],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }

    /// <summary>大理石鳞甲（Marble Scalemail）：9s 中 铜 服饰，获得 20 » 60 » 120 » 200 护盾。</summary>
    public static ItemTemplate MarbleScalemail()
    {
        return new ItemTemplate
        {
            Name = "大理石鳞甲",
            Desc = "▶ 获得 {Shield} 护盾",
            Tags = [Tag.Apparel],
            Cooldown = 9.0,
            Shield = [20, 60, 120, 200],
            Abilities =
            [
                Ability.Shield,
            ],
        };
    }

    /// <summary>废品场大棒（Junkyard Club）：11s 中 铜 武器，造成 30 » 60 » 120 » 240 伤害。</summary>
    public static ItemTemplate JunkyardClub()
    {
        return new ItemTemplate
        {
            Name = "废品场大棒",
            Desc = "▶ 造成 {Damage} 伤害",
            Tags = [Tag.Weapon],
            Cooldown = 11.0,
            Damage = [30, 60, 120, 240],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }

    /// <summary>火箭靴（Rocket Boots）：5s 中 铜 工具 服饰，加速相邻物品 1 » 2 » 3 » 4 秒（优先级 High）。</summary>
    public static ItemTemplate RocketBoots()
    {
        return new ItemTemplate
        {
            Name = "火箭靴",
            Desc = "▶ 加速相邻物品 {HasteSeconds} 秒",
            Tags = [Tag.Tool, Tag.Apparel],
            Cooldown = 5.0,
            Haste = [1.0, 2.0, 3.0, 4.0],
            HasteTargetCount = 2,
            Abilities =
            [
                Ability.Haste.Override(
                    additionalTargetCondition: Condition.AdjacentToSource,
                    priority: AbilityPriority.High
                ),
            ],
        };
    }

    /// <summary>火蜥幼兽（Salamander Pup）：8s 中 铜 伙伴，造成 4 » 6 » 8 » 10 灼烧。</summary>
    public static ItemTemplate SalamanderPup()
    {
        return new ItemTemplate
        {
            Name = "火蜥幼兽",
            Desc = "▶ 造成 {Burn} 灼烧",
            Tags = [Tag.Friend],
            Cooldown = 8.0,
            Burn = [4, 6, 8, 10],
            Abilities =
            [
                Ability.Burn,
            ],
        };
    }

    /// <summary>简易路障（Makeshift Barricade）：7s 中 铜，减速 1 件物品 1 » 2 » 3 » 4 秒。</summary>
    public static ItemTemplate MakeshiftBarricade()
    {
        return new ItemTemplate
        {
            Name = "简易路障",
            Desc = "▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒",
            Tags = [],
            Cooldown = 7.0,
            Slow = [1.0, 2.0, 3.0, 4.0],
            Abilities =
            [
                Ability.Slow,
            ],
        };
    }

    /// <summary>外骨骼（Exoskeleton）：中 铜 服饰，相邻武器 +5 » +10 » +20 » +40 伤害。</summary>
    public static ItemTemplate Exoskeleton()
    {
        return new ItemTemplate
        {
            Name = "外骨骼",
            Desc = "▶ 相邻武器 {+Custom_0} 伤害",
            Tags = [Tag.Apparel],
            Custom_0 = [5, 10, 20, 40],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.Damage,
                    Condition = Condition.AdjacentToSource & Condition.WithTag(Tag.Weapon),
                    Value = Formula.Source(Key.Custom_0),
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
            Desc = "▶ 修复 {RepairTargetCount} 件物品；治疗 {Heal} 生命值",
            Tags = [Tag.Friend, Tag.Tech],
            Cooldown = 5.0,
            Heal = [30, 60, 120, 240],
            Abilities =
            [
                Ability.Repair.Override(
                    priority: AbilityPriority.Lowest
                ),
                Ability.Heal,
            ],
        };
    }

    /// <summary>宇宙炫羽（Cosmic Plume）：4s 中 银 遗物；1 件物品开始飞行；使用物品时飞行物品暴击率 +5% » +10% » +15%（限本场战斗）；造成暴击或使用飞行物品时，为此物品充能 1 秒（Low，Also 表示两条件）。</summary>
    public static ItemTemplate CosmicPlume()
    {
        return new ItemTemplate
        {
            Name = "宇宙炫羽",
            Desc = "▶ {ModifyAttributeTargetCount} 件物品开始飞行；飞行物品暴击率 {+Custom_0%}；造成暴击或使用飞行物品时，为此物品充能 {ChargeSeconds} 秒",
            Tags = [Tag.Relic],
            Cooldown = 4.0,
            Custom_0 = [5, 10, 15],
            Charge = 1.0,
            ModifyAttributeTargetCount = 1,
            Abilities =
            [
                Ability.StartFlying,
                Ability.AddAttribute(Key.CritRatePercent).Override(
                    additionalTargetCondition: Condition.InFlight,
                    priority: AbilityPriority.Low
                ),
                Ability.Charge.Override(
                    trigger: Trigger.Crit,
                    targetCondition: Condition.SameAsSource,
                    priority: AbilityPriority.Low
                ).Also(
                    trigger: Trigger.UseItem,
                    condition: Condition.SameSide & Condition.InFlight
                ),
            ],
        };
    }

    /// <summary>巨龙翼（Dragon Wing）：7s 中 银 巨龙，获得 40 » 60 » 80 护盾；1 件物品开始飞行；触发灼烧时，为此物品充能 2 秒（Low）。</summary>
    public static ItemTemplate DragonWing()
    {
        return new ItemTemplate
        {
            Name = "巨龙翼",
            Desc = "▶ 获得 {Shield} 护盾；▶ {ModifyAttributeTargetCount} 件物品开始飞行；触发灼烧时，为此物品充能 {ChargeSeconds} 秒",
            Tags = [Tag.Dragon],
            Cooldown = 7.0,
            Shield = [40, 60, 80],
            Charge = 2.0,
            ModifyAttributeTargetCount = 1,
            Abilities =
            [
                Ability.Shield,
                Ability.StartFlying,
                Ability.Charge.Override(
                    trigger: Trigger.Burn,
                    targetCondition: Condition.SameAsSource,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }

    /// <summary>碾骨爪（Crusher Claw）：9s 中 银 武器 水系，造成伤害等量于己方物品中最高的护盾值；护盾物品的护盾提高 +20 » +40 » +60（限本场战斗）（High）。</summary>
    public static ItemTemplate CrusherClaw()
    {
        return new ItemTemplate
        {
            Name = "碾骨爪",
            Desc = "▶ 造成伤害，等量于己方物品中最高的护盾值；▶ 护盾物品的护盾提高 {+Custom_0}（限本场战斗）",
            Tags = [Tag.Weapon, Tag.Aquatic],
            Cooldown = 9.0,
            Custom_0 = [20, 40, 60],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.Damage,
                    Value = Formula.SideSelect(Key.Shield, SideSelectKind.Max),
                },
            ],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Shield).Override(
                    additionalTargetCondition: Condition.WithTag(Tag.Shield),
                    priority: AbilityPriority.High
                ),
            ],
        };
    }

    /// <summary>寒冰特服（Cryosleeve）：4s 中 银 服饰，▶冻结此物品以及相邻物品1秒；任意物品冻结时，获得50 » 75 » 100护盾；己方物品受到的冻结效果时长减半（光环）。</summary>
    public static ItemTemplate Cryosleeve()
    {
        return new ItemTemplate
        {
            Name = "寒冰特服",
            Desc = "▶ 冻结此物品以及相邻物品 {FreezeSeconds} 秒；任意物品冻结时，获得 {Shield} 护盾；己方物品受到的冻结效果时长减半",
            Tags = [Tag.Apparel],
            Cooldown = 4.0,
            Freeze = 1.0,
            FreezeTargetCount = 3,
            Shield = [50, 75, 100],
            Abilities =
            [
                Ability.Freeze.Override(
                    targetCondition: Condition.SameAsSource | Condition.AdjacentToSource
                ),
                Ability.Shield.Override(
                    trigger: Trigger.Freeze,
                    targetCondition: Condition.SameAsSource
                ),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.PercentFreezeReduction,
                    Condition = Condition.SameSide,
                    Value = Formula.Constant(50),
                },
            ],
        };
    }

    /// <summary>守护神之壳（Guardian Shell）：7s 中 银 遗物，▶获得40 » 60 » 80护盾；触发剧毒时，为此物品充能2秒（Low）。</summary>
    public static ItemTemplate GuardianShell()
    {
        return new ItemTemplate
        {
            Name = "守护神之壳",
            Desc = "▶ 获得 {Shield} 护盾；触发剧毒时，为此物品充能 {ChargeSeconds} 秒",
            Tags = [Tag.Relic],
            Cooldown = 7.0,
            Shield = [40, 60, 80],
            Charge = 2.0,
            Abilities =
            [
                Ability.Shield,
                Ability.Charge.Override(
                    trigger: Trigger.Poison,
                    targetCondition: Condition.SameAsSource,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }

    /// <summary>破冰尖镐（Icebreaker）：7s 中 银 武器 工具，造成 100 » 200 » 300 伤害；解除己方物品的冻结效果（剩余冻结时间减去 1000 秒）；任意物品冻结时为此物品充能 1 » 2 » 3 秒（Low）；此物品冻结时解除其冻结效果（Low）。</summary>
    public static ItemTemplate Icebreaker()
    {
        return new ItemTemplate
        {
            Name = "破冰尖镐",
            Desc = "▶ 造成 {Damage} 伤害；▶ 解除己方物品的冻结效果；任意物品冻结时，为此物品充能 {ChargeSeconds} 秒；此物品冻结时，解除其冻结效果",
            Tags = [Tag.Weapon, Tag.Tool],
            Cooldown = 7.0,
            Damage = [100, 200, 300],
            Charge = [1.0, 2.0, 3.0],
            Abilities =
            [
                Ability.Damage,
                Ability.ReduceAttributeCaster(Key.FreezeRemainingMs).Override(
                    value: 1_000_000,
                    effectLogName: "解除冻结",
                    priority: AbilityPriority.Medium
                ),
                Ability.Charge.Override(
                    trigger: Trigger.Freeze,
                    targetCondition: Condition.SameAsSource,
                    priority: AbilityPriority.Low
                ),
                Ability.ReduceAttributeCaster(Key.FreezeRemainingMs).Override(
                    trigger: Trigger.Freeze,
                    invokeTargetCondition: Condition.SameAsSource,
                    targetCondition: Condition.SameAsSource,
                    value: 1_000_000,
                    effectLogName: "解除冻结",
                    priority: AbilityPriority.Low
                ),
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

        db.DefaultMinTier = ItemTier.Silver;
        db.Register(CosmicPlume());
        db.Register(DragonWing());
        db.Register(CrusherClaw());
        db.Register(Cryosleeve());
        db.Register(GuardianShell());
        db.Register(Icebreaker());
    }
}
