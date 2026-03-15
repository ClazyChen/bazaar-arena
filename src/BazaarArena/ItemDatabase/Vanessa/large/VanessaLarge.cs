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
        // db.Register(...);
    }
}
