using BazaarArena.Core;

namespace BazaarArena.ItemDatabase;

/// <summary>公共大型物品模板：供卡组与模拟共用的常见大型物品。</summary>
public static class CommonLarge
{
    /// <summary>临时避难所（Temporary Shelter）：7s 铜 大 地产，获得 10 » 20 » 40 » 80 护盾。</summary>
    public static ItemTemplate TemporaryShelter()
    {
        return new ItemTemplate
        {
            Name = "临时避难所",
            Desc = "▶ 获得 {Shield} 护盾",
            Tags = [Tag.Property],
            Cooldown = 7.0,
            Shield = [10, 20, 40, 80],
            Abilities =
            [
                Ability.Shield,
            ],
        };
    }

    /// <summary>哈库维发射器（Harkuvian Launcher）：3s 铜 大 武器，造成 100 » 200 » 300 » 400 伤害；弹药：1。</summary>
    public static ItemTemplate HarkuvianLauncher()
    {
        return new ItemTemplate
        {
            Name = "哈库维发射器",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}",
            Tags = [Tag.Weapon],
            Cooldown = 3.0,
            Damage = [100, 200, 300, 400],
            AmmoCap = 1,
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }

    /// <summary>观光缆车（Tourist Chariot）：5s 铜 大 载具，获得 20 » 40 » 80 » 160 护盾。</summary>
    public static ItemTemplate TouristChariot()
    {
        return new ItemTemplate
        {
            Name = "观光缆车",
            Desc = "▶ 获得 {Shield} 护盾",
            Tags = [Tag.Vehicle],
            Cooldown = 5.0,
            Shield = [20, 40, 80, 160],
            Abilities =
            [
                Ability.Shield,
            ],
        };
    }

    /// <summary>温泉（Hot Springs）：6s 铜 大 地产，治疗 25 » 50 » 100 » 200 生命值。</summary>
    public static ItemTemplate HotSprings()
    {
        return new ItemTemplate
        {
            Name = "温泉",
            Desc = "▶ 治疗 {Heal} 生命值",
            Tags = [Tag.Property],
            Cooldown = 6.0,
            Heal = [25, 50, 100, 200],
            Abilities =
            [
                Ability.Heal,
            ],
        };
    }

    /// <summary>废品场长枪（Junkyard Lance）：11s 铜 大 武器，每拥有一件小型物品（含储存箱等效）造成 15 » 30 » 60 » 100 伤害；Damage 基础 0，由光环按 Custom_0 * (卡组小型物品数 + StashParameter) 增加。</summary>
    public static ItemTemplate JunkyardLance()
    {
        return new ItemTemplate
        {
            Name = "废品场长枪",
            Desc = "▶ 每拥有一件小型物品（含储存箱等效）造成 {Custom_0} 伤害",
            Tags = [Tag.Weapon],
            Cooldown = 11.0,
            Damage = 0,
            Custom_0 = [15, 30, 60, 100],
            StashParameter = [1, 2, 3, 4],
            OverridableAttributes = new Dictionary<string, IntOrByTier> { 
                [Key.StashParameter] = [1, 2, 3, 4] 
            },
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.Damage,
                    Value = Formula.Source(Key.Custom_0) * (Formula.Source(Key.StashParameter) + Formula.Count(Condition.SameSide & Condition.WithTag(Tag.Small))),
                },
            ],
        };
    }

    /// <summary>废品场弹射机（Junkyard Catapult）：7s 银 大 武器，造成 25 » 50 » 100 伤害；造成 6 » 8 » 10 灼烧；造成 4 » 6 » 8 剧毒；弹药：1。</summary>
    public static ItemTemplate JunkyardCatapult()
    {
        return new ItemTemplate
        {
            Name = "废品场弹射机",
            Desc = "▶ 造成 {Damage} 伤害；▶ 造成 {Burn} 灼烧；▶ 造成 {Poison} 剧毒；弹药：{AmmoCap}",
            Tags = [Tag.Weapon],
            Cooldown = 7.0,
            Damage = [25, 50, 100],
            Burn = [6, 8, 10],
            Poison = [4, 6, 8],
            AmmoCap = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.Burn,
                Ability.Poison,
            ],
        };
    }

    /// <summary>巨型冰棒（Colossal Popsicle）：9s 银 大 武器，造成 50 » 100 » 200 伤害；冻结 2 件物品 1 » 2 » 3 秒。</summary>
    public static ItemTemplate ColossalPopsicle()
    {
        return new ItemTemplate
        {
            Name = "巨型冰棒",
            Desc = "▶ 造成 {Damage} 伤害；▶ 冻结 {FreezeTargetCount} 件物品 {FreezeSeconds} 秒",
            Tags = [Tag.Weapon, Tag.Food],
            Cooldown = 9.0,
            Damage = [50, 100, 200],
            FreezeTargetCount = 2,
            Freeze = [1.0, 2.0, 3.0],
            Abilities =
            [
                Ability.Damage,
                Ability.Freeze,
            ],
        };
    }

    /// <summary>以太能量导体（Ethergy Conduit）：大 金 遗物；触发剧毒或使用遗物时，己方物品暴击率 +2% » +4%（限本场战斗）（使用遗物时优先级 Medium，触发剧毒且来源不是遗物时优先级 Low）；造成暴击时，为己方遗物充能 1 秒（Low）。</summary>
    public static ItemTemplate EthergyConduit()
    {
        return new ItemTemplate
        {
            Name = "以太能量导体",
            Desc = "触发剧毒或使用遗物时，己方物品暴击率 {+Custom_0%}（限本场战斗）；造成暴击时，为己方遗物充能 {ChargeSeconds} 秒",
            Tags = [Tag.Relic],
            Cooldown = 0,
            Custom_0 = [2, 4],
            Charge = 1.0,
            Abilities =
            [
                Ability.AddAttribute(Key.CritRatePercent).Override(
                    trigger: Trigger.UseItem,
                    condition: Condition.SameSide & Condition.WithTag(Tag.Relic),
                    priority: AbilityPriority.Medium
                ),
                Ability.AddAttribute(Key.CritRatePercent).Override(
                    trigger: Trigger.Poison,
                    condition: Condition.NotWithTag(Tag.Relic),
                    priority: AbilityPriority.Low
                ),
                Ability.Charge.Override(
                    trigger: Trigger.Crit,
                    additionalTargetCondition: Condition.WithTag(Tag.Relic),
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }

    /// <summary>焰形剑（Flamberge）：9s 钻 大 武器；▶ 造成 200 伤害；此物品能造成四倍暴击伤害（光环：自身暴击伤害 +300%）。</summary>
    public static ItemTemplate Flamberge()
    {
        return new ItemTemplate
        {
            Name = "焰形剑",
            Desc = "▶ 造成 {Damage} 伤害；此物品能造成四倍暴击伤害",
            Tags = [Tag.Weapon],
            Cooldown = 9.0,
            Damage = 200,
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.CritDamagePercent,
                    Value = Formula.Constant(300),
                    Percent = true,
                },
            ],
        };
    }

    /// <summary>注册所有公共大型物品到数据库。</summary>
    public static void RegisterAll(ItemDatabase db)
    {
        db.DefaultSize = ItemSize.Large;
        db.DefaultHero = Hero.Common;
        db.DefaultMinTier = ItemTier.Bronze;
        db.Register(TemporaryShelter());
        db.Register(HarkuvianLauncher());
        db.Register(TouristChariot());
        db.Register(HotSprings());
        db.Register(JunkyardLance());

        db.DefaultMinTier = ItemTier.Silver;
        db.Register(JunkyardCatapult());
        db.Register(ColossalPopsicle());

        db.DefaultMinTier = ItemTier.Gold;
        db.Register(EthergyConduit());

        db.DefaultMinTier = ItemTier.Diamond;
        db.Register(Flamberge());
    }
}
