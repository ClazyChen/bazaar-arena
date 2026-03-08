namespace BazaarArena.Core;

/// <summary>卡组：从左到右的槽位条目，玩家等级与槽位上限、生命上限等。</summary>
public class Deck
{
    /// <summary>玩家等级（1–20），影响槽位上限与可选物品等级。</summary>
    public int PlayerLevel { get; set; } = 1;

    /// <summary>从左到右的物品槽位；总占用槽数不得超过 <see cref="MaxSlotsForLevel"/>。</summary>
    public List<DeckSlotEntry> Slots { get; set; } = new();

    /// <summary>玩家字段重写：如初始生命上限、护盾、生命再生等。默认收入 7，金钱 15。</summary>
    public Dictionary<string, int>? PlayerOverrides { get; set; }

    /// <summary>根据玩家等级返回槽位上限：1–2 级 4 槽，3 级 6 槽，4 级 8 槽，5+ 级 10 槽。</summary>
    public static int MaxSlotsForLevel(int level)
    {
        return level switch
        {
            1 or 2 => 4,
            3 => 6,
            4 => 8,
            _ => 10,
        };
    }

    /// <summary>银物品至少 3 级，金至少 7 级，钻石至少 10 级。</summary>
    public static bool TierAllowedForLevel(ItemTier tier, int level)
    {
        return tier switch
        {
            ItemTier.Bronze => true,
            ItemTier.Silver => level >= 3,
            ItemTier.Gold => level >= 7,
            ItemTier.Diamond => level >= 10,
            _ => false,
        };
    }
}
