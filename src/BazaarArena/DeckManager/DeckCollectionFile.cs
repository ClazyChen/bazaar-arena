using BazaarArena.Core;

namespace BazaarArena.DeckManager;

/// <summary>卡组集文件格式：一个 JSON 文件包含多个卡组。</summary>
public class DeckCollectionFile
{
    public string? Version { get; set; } = "1.0";
    public List<DeckEntry> Decks { get; set; } = [];
}

/// <summary>卡组集中的单条：ID + 卡组数据。</summary>
public class DeckEntry
{
    public string Id { get; set; } = "";
    public Deck Deck { get; set; } = new();
}
