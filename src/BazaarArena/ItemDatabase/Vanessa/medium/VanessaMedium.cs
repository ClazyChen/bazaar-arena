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
        db.DefaultMinTier = ItemTier.Silver;
        db.Register(SharkClaws.Template());
        db.DefaultMinTier = ItemTier.Bronze;
        db.Register(SharkClaws.Template_S2());
        db.DefaultMinTier = ItemTier.Silver;
        db.Register(Wetware.Template());
        db.Register(VolcanicVents.Template());
        db.Register(CoralArmor.Template());
    }
}
