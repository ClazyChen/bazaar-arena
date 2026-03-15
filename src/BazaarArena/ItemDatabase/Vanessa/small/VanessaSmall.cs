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
        db.Register(ConcealedDagger.Template());
        db.Register(Piranha.Template());
        db.Register(Piranha.Template_S1());
        db.Register(Calico.Template());
        db.Register(Calico.Template_S1());
        db.Register(HoningSteel.Template());
        db.Register(HoningSteel.Template_S9());
        db.Register(HoningSteel.Template_S1());

        db.DefaultMinTier = ItemTier.Silver;
        db.Register(BilgeWorm.Template_S10());
        db.Register(BilgeWorm.Template_S9());
        db.Register(ConcealedDagger.Template_S1());
    }
}
