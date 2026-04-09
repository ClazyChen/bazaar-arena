using BazaarArena.Core;

namespace BazaarArena.GreedyDeckFinder;

/// <summary>
/// Greedy 专用：玩家等级与 Vanessa 池 MinTier、战斗扁平化档位、Overridable 预缩放。
/// 与 <see cref="Deck.TierAllowedForLevel"/>（GUI 卡组）门槛不同，勿混用。
/// </summary>
public static class GreedyLevelRules
{
    public const int MinPlayerLevel = 2;
    public const int MaxPlayerLevel = 20;

    /// <summary>模板 <see cref="ItemTemplate.MinTier"/> 是否可进入当前等级的 Greedy 物品池。</summary>
    public static bool IsMinTierAllowedInPool(ItemTier templateMinTier, int playerLevel)
    {
        return templateMinTier switch
        {
            ItemTier.Bronze => true,
            ItemTier.Silver => playerLevel >= 5,
            ItemTier.Gold => playerLevel >= 8,
            ItemTier.Diamond => playerLevel >= 11,
            _ => false,
        };
    }

    /// <summary>扁平化模板读数与 <see cref="BattleSimulator.ItemState"/> / 卡组槽位 <see cref="DeckSlotEntry.Tier"/> 使用的档位。</summary>
    public static ItemTier CombatTier(int playerLevel)
    {
        if (playerLevel <= 4) return ItemTier.Bronze;
        if (playerLevel <= 7) return ItemTier.Silver;
        if (playerLevel <= 10) return ItemTier.Gold;
        return ItemTier.Diamond;
    }

    /// <summary>对 <see cref="ItemTemplate.OverridableAttributes"/> 中某一 key 按玩家等级写入战斗扁平模板的整数值。</summary>
    public static int ComputeOverridableValue(ItemTemplate source, int key, int playerLevel)
    {
        int bronzeVal = source.GetInt(key, ItemTier.Bronze);
        int silverVal = source.GetInt(key, ItemTier.Silver);
        int goldVal = source.GetInt(key, ItemTier.Gold);
        int diamondVal = source.GetInt(key, ItemTier.Diamond);

        if (playerLevel <= 1)
            return bronzeVal / 2;

        return playerLevel switch
        {
            2 => bronzeVal / 2,
            3 => bronzeVal,
            4 or 5 => (bronzeVal + silverVal) / 2,
            6 => silverVal,
            7 or 8 => (silverVal + goldVal) / 2,
            9 => goldVal,
            10 or 11 => (goldVal + diamondVal) / 2,
            12 => diamondVal,
            _ => diamondVal + (playerLevel - 12) * (diamondVal - goldVal) / 2,
        };
    }
}
