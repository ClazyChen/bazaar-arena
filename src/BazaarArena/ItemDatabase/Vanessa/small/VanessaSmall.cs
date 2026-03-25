using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>海盗 Vanessa 关卡专属小型物品注册入口。</summary>
public static class VanessaSmall
{
    /// <summary>注册所有 Vanessa 小型物品到数据库。</summary>
    public static void RegisterAll(ItemDatabase db)
    {
        db.DefaultSize = ItemSize.Small;
        db.DefaultHero = Hero.Vanessa;
        db.DefaultMinTier = ItemTier.Bronze;
        db.Register(BilgeWorm.Template());
        db.Register(Clamera.Template_S8());
        db.Register(Clamera.Template_S1());
        db.Register(ConcealedDagger.Template());
        db.Register(Piranha.Template());
        db.Register(Piranha.Template_S1());
        db.Register(Calico.Template());
        db.Register(Calico.Template_S1());
        db.Register(HoningSteel.Template());
        db.Register(HoningSteel.Template_S9());
        db.Register(HoningSteel.Template_S1());
        db.Register(Narwhal.Template());
        db.Register(Narwhal.Template_S1());
        db.Register(Chum.Template());
        db.Register(Coral.Template());
        db.Register(IllusoRay.Template());
        db.Register(Lighter.Template());
        db.Register(Shuriken.Template());
        db.Register(Bayonet.Template());
        db.Register(PetRock.Template());
        db.Register(Revolver.Template());
        db.Register(Revolver.Template_S1());
        db.Register(Handaxe.Template());
        db.Register(Grenade.Template());
        db.Register(GrapplingHook.Template());
        db.Register(GrapplingHook.Template_S1());
        db.Register(Seaweed.Template());
        db.Register(Bolas.Template());
        db.Register(Bolas.Template_S1());
        db.Register(SeaShell.Template());
        db.Register(PopSnappers.Template());
        db.Register(Pearl.Template());
        db.Register(Zoarcid.Template());
        db.Register(Zoarcid.Template_S6());
        db.Register(Zoarcid.Template_S4());
        db.Register(Grapeshot.Template());
        db.Register(TinyCutlass.Template());
        db.Register(ShoeBlade.Template());
        db.Register(ShoeBlade.Template_S1());
        db.Register(Ambergris.Template());
        db.Register(Switchblade.Template());
        db.Register(Switchblade.Template_S1());
        db.Register(Jellyfish.Template());
        db.Register(PowderHorn.Template());
        db.Register(PeskyPete.Template());
        db.Register(Catfish.Template());
        db.Register(MantisShrimp.Template());
        db.Register(YetiCrab.Template());

        db.DefaultMinTier = ItemTier.Silver;
        db.Register(BilgeWorm.Template_S10());
        db.Register(BilgeWorm.Template_S9());
        db.Register(ConcealedDagger.Template_S1());
        db.Register(DiveWeights.Template());
        db.Register(Kusarigama.Template());
        db.Register(Kusarigama.Template_S4());
        db.Register(CustomScope.Template());
        db.Register(CustomScope.Template_S2());
        db.Register(IcePick.Template());
        db.Register(Clamera.Template());
        db.Register(Grapeshot.Template_S1());
        db.Register(PeskyPete.Template_S5());

        db.DefaultMinTier = ItemTier.Gold;
        db.Register(PeskyPete.Template_S1());

    }
}
