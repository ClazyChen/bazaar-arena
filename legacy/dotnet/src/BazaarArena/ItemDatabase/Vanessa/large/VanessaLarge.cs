using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

/// <summary>海盗 Vanessa 关卡专属大型物品注册入口。</summary>
public static class VanessaLarge
{
    /// <summary>注册所有 Vanessa 大型物品到数据库。</summary>
    public static void RegisterAll(ItemDatabase db)
    {
        db.DefaultSize = ItemSize.Large;
        db.DefaultHero = Hero.Vanessa;
        db.DefaultMinTier = ItemTier.Bronze;
        db.Register(Cove.Template());

        db.DefaultMinTier = ItemTier.Silver;
        db.Register(SeadogsSaloon.Template());
        db.Register(Port.Template());
        db.Register(Port.Template_S10());
        db.Register(Submarine.Template());
        db.Register(CrowsNest.Template());
        db.Register(Flagship.Template());
        db.Register(Trebuchet.Template());
        db.Register(WaterWheel.Template());
        db.Register(CaptainsQuarters.Template());

        db.DefaultMinTier = ItemTier.Gold;
        db.Register(StealthGlider.Template());
        db.Register(TheBoulder.Template());
        db.Register(Tortuga.Template());
        db.Register(Dam.Template());
        db.Register(Ballista.Template());
        db.Register(SlumberingPrimordial.Template());
        db.Register(Cannonade.Template());
        db.Register(ElectricEels.Template());
        db.Register(Lighthouse.Template());
        db.Register(TropicalIsland.Template());

        db.DefaultMinTier = ItemTier.Diamond;
        db.Register(Iceberg.Template());
        db.Register(Shipwreck.Template());
    }
}
