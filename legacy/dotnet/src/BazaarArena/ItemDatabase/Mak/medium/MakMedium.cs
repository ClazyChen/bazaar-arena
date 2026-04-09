using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

/// <summary>英雄 Mak 关卡专属中型物品注册入口。</summary>
public static class MakMedium
{
    /// <summary>注册所有 Mak 中型物品到数据库。</summary>
    public static void RegisterAll(ItemDatabase db)
    {
        db.DefaultSize = ItemSize.Medium;
        db.DefaultHero = Hero.Mak;

        db.DefaultMinTier = ItemTier.Bronze;
        db.Register(PotionPotion.Template());
        db.Register(SwordCane.Template());
        db.Register(Cellar.Template());
        db.Register(ShowGlobe.Template());
        db.Register(SandsOfTime.Template());
        db.Register(Peacewrought.Template());
        db.Register(Refractor.Template());
        db.Register(Retort.Template());
        db.Register(Leeches.Template());
        db.Register(EternalTorch.Template());
        db.Register(Aludel.Template());
        db.Register(Calcinator.Template());
        db.Register(LifeConduit.Template());
        db.Register(MortarAndPestle.Template());
        db.Register(BlankSlate.Template());
        db.Register(IdolOfDecay.Template());
        db.Register(Candles.Template());
        db.Register(CovetousRaven.Template());
        db.Register(Nightshade.Template());
        db.Register(MagicCarpet.Template());
    }
}

