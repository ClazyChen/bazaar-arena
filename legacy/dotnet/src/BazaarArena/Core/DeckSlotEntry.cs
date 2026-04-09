namespace BazaarArena.Core;

/// <summary>卡组中的一个槽位条目：物品名称、等级，以及可选的局外重写。</summary>
public class DeckSlotEntry
{
    /// <summary>物品中文名称，用于从物品数据库创建实例。</summary>
    public string ItemName { get; set; } = "";

    /// <summary>该物品在卡组中的等级。</summary>
    public ItemTier Tier { get; set; }

    /// <summary>局外成长等对属性的重写，键为属性名，值为数值（如暴击率 20、40、60）。</summary>
    public Dictionary<string, int>? Overrides { get; set; }
}
