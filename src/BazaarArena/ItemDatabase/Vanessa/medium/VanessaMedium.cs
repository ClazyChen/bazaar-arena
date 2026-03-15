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
        // db.Register(...);
    }
}
