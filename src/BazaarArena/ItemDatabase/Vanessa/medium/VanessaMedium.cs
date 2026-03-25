using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>海盗 Vanessa 关卡专属中型物品注册入口。</summary>
public static class VanessaMedium
{
    /// <summary>注册所有 Vanessa 中型物品到数据库。</summary>
    public static void RegisterAll(ItemDatabase db)
    {
        db.DefaultSize = ItemSize.Medium;
        db.DefaultHero = Hero.Vanessa;
        db.DefaultMinTier = ItemTier.Bronze;
        db.Register(FishingNet.Template());
        db.Register(FishingNet.Template_S7());
        db.Register(FishingNet.Template_S1());
        db.Register(LifePreserver.Template());
        db.Register(DoubleBarrel.Template());
        db.Register(Cutlass.Template());
        db.Register(Barrel.Template());
        db.Register(Barrel.Template_S1());
        db.Register(Rifle.Template());
        db.Register(Rifle.Template_S1());
        db.Register(Katana.Template());
        db.Register(Langxian.Template());
        db.Register(BeachBall.Template());
        db.Register(FishingRod.Template());
        db.Register(Shovel.Template_S1());
        db.Register(Shovel.Template());
        db.Register(StarChart.Template());
        db.Register(Cannon.Template());
        db.Register(Wetware.Template());
        db.Register(VolcanicVents.Template());
        db.Register(CoralArmor.Template());
        db.Register(SharkClaws.Template());
        db.Register(SharkClaws.Template_S2());
        db.Register(Sharkray.Template_S11());

        db.DefaultMinTier = ItemTier.Silver;
        db.Register(Disguise.Template());
        db.Register(Disguise.Template_S1());
        db.Register(Sharkray.Template());
        db.Register(ScimitarOfTheDeep.Template());
        db.Register(ScimitarOfTheDeep.Template_S9());
        db.Register(Bonfire.Template());
        db.Register(CyberSai.Template());
        db.Register(CyberSai.Template_S6());

        db.Register(BladedHoverboard.Template());
        db.Register(BladedHoverboard.Template_S4());
        db.Register(OrangeJulian.Template());
        db.Register(OrangeJulian.Template_S8());
        db.Register(ElementalDepthCharge.Template());
        db.Register(Sextant.Template());
        db.Register(Sextant.Template_S1());
        db.Register(Ramrod.Template());
        db.Register(Ramrod.Template_S5());
        db.Register(Ramrod.Template_S1());
        db.Register(Javelin.Template());
        db.Register(Javelin.Template_S11());
        db.Register(Javelin.Template_S5());
        db.Register(Submersible.Template());
        db.Register(Submersible.Template_S8());
        db.Register(Arbalest.Template());
        db.Register(Musket.Template());
        db.Register(Musket.Template_S4());
        db.Register(Repeater.Template());
        db.Register(Repeater.Template_S1());
        db.Register(Harpoon.Template());
        db.Register(Torpedo.Template());
        db.Register(Torpedo.Template_S1());
        db.Register(WantedPoster.Template());
        db.Register(Astrolabe.Template());
        db.Register(Astrolabe.Template_S1());
        db.Register(WeatherGlass.Template());
        db.Register(WeatherGlass.Template_S4());
        db.Register(Lockbox.Template());
        db.Register(OniMask.Template());
        db.Register(CauterizingBlade.Template());
    }
}
