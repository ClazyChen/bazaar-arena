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
            Desc = "获得 {Shield} 护盾",
            Tags = [Tag.Property],
            Cooldown = 7.0,
            Shield = [10, 20, 40, 80],
            Abilities =
            [
                Ability.Shield(),
            ],
        };
    }

    /// <summary>哈库维发射器（Harkuvian Launcher）：3s 铜 大 武器，造成 100 » 200 » 300 » 400 伤害；弹药：1。</summary>
    public static ItemTemplate HarkuvianLauncher()
    {
        return new ItemTemplate
        {
            Name = "哈库维发射器",
            Desc = "造成 {Damage} 伤害；弹药：{AmmoCap}",
            Tags = [Tag.Weapon],
            Cooldown = 3.0,
            Damage = [100, 200, 300, 400],
            AmmoCap = 1,
            Abilities =
            [
                Ability.Damage(),
            ],
        };
    }

    /// <summary>观光缆车（Tourist Chariot）：5s 铜 大 载具，获得 20 » 40 » 80 » 160 护盾。</summary>
    public static ItemTemplate TouristChariot()
    {
        return new ItemTemplate
        {
            Name = "观光缆车",
            Desc = "获得 {Shield} 护盾",
            Tags = [Tag.Vehicle],
            Cooldown = 5.0,
            Shield = [20, 40, 80, 160],
            Abilities =
            [
                Ability.Shield(),
            ],
        };
    }

    /// <summary>温泉（Hot Springs）：6s 铜 大 地产，治疗 25 » 50 » 100 » 200 生命值。</summary>
    public static ItemTemplate HotSprings()
    {
        return new ItemTemplate
        {
            Name = "温泉",
            Desc = "治疗 {Heal} 生命值",
            Tags = [Tag.Property],
            Cooldown = 6.0,
            Heal = [25, 50, 100, 200],
            Abilities =
            [
                Ability.Heal(),
            ],
        };
    }

    /// <summary>废品场长枪（Junkyard Lance）：11s 铜 大 武器，每拥有一件小型物品（含储存箱等效）造成 15 » 30 » 60 » 100 伤害；Damage 基础 0，由光环按 Custom_0 * (卡组小型物品数 + StashParameter) 增加。</summary>
    public static ItemTemplate JunkyardLance()
    {
        return new ItemTemplate
        {
            Name = "废品场长枪",
            Desc = "每拥有一件小型物品（含储存箱等效）造成 {Custom_0} 伤害",
            Tags = [Tag.Weapon],
            Cooldown = 11.0,
            Damage = 0,
            Custom_0 = [15, 30, 60, 100],
            StashParameter = [1, 2, 3, 4],
            OverridableAttributes = new Dictionary<string, IntOrByTier> { 
                [nameof(ItemTemplate.StashParameter)] = [1, 2, 3, 4] 
            },
            Abilities =
            [
                Ability.Damage(),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = nameof(ItemTemplate.Damage),
                    FixedValueFormula = Formula.SmallCountStash,
                },
            ],
        };
    }

    /// <summary>注册所有公共大型物品到数据库。</summary>
    public static void RegisterAll(ItemDatabase db)
    {
        db.DefaultSize = ItemSize.Large;
        db.DefaultMinTier = ItemTier.Bronze;
        db.Register(TemporaryShelter());
        db.Register(HarkuvianLauncher());
        db.Register(TouristChariot());
        db.Register(HotSprings());
        db.Register(JunkyardLance());
    }
}
