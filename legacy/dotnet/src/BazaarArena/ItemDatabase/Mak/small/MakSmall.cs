using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

/// <summary>英雄 Mak 关卡专属小型物品注册入口。</summary>
public static class MakSmall
{
    /// <summary>注册所有 Mak 小型物品到数据库。</summary>
    public static void RegisterAll(ItemDatabase db)
    {
        db.DefaultSize = ItemSize.Small;
        db.DefaultHero = Hero.Mak;

        db.DefaultMinTier = ItemTier.Bronze;
        db.Register(BarbedClaws.Template());
        db.Register(RegenerationPotion.Template());
        db.Register(NoxiousPotion.Template());
        db.Register(SmellingSalts.Template());
        db.Register(FloorSpike.Template());
        db.Register(TazidianDagger.Template());
        db.Register(RainbowPotion.Template());
        db.Register(Scalpel.Template());
        db.Register(LetterOpener.Template());
        db.Register(SleepingPotion.Template());
        db.Register(Venom.Template());
        db.Register(VenomousDose.Template());
        db.Register(CloudWisp.Template());
        db.Register(Catalyst.Template());
        db.Register(Hemlock.Template());
        db.Register(Venomander.Template());
        db.Register(Myrrh.Template());
        db.Register(FirePotion.Template());
        db.Register(Incense.Template());
        db.Register(Orly.Template());
        db.Register(BottledLightning.Template());
        db.Register(FungalSpores.Template());
        db.Register(Sulphur.Template());
        db.Register(BrokenBottle.Template());
        db.Register(IonizedLightning.Template());
        db.Register(QuillAndInk.Template());
        db.Register(Ruby.Template());
        db.Register(Emerald.Template());
        db.Register(Fireflies.Template());
        db.Register(BasiliskFang.Template());
        db.Register(PhilosophersStone.Template());
        db.Register(Moss.Template());
        db.Register(Mothmeal.Template());
        db.Register(Thurible.Template());
        db.Register(CrocodileTears.Template());
        db.Register(ShardOfObsidian.Template());
        db.Register(BlackRose.Template());
    }
}

