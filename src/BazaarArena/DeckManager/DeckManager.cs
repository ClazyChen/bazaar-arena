using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;

namespace BazaarArena.DeckManager;

/// <summary>卡组管理器：维护当前卡组集（内存），支持从/向单个 JSON 文件打开与保存。</summary>
public class DeckManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly Dictionary<string, Deck> _decks = [];
    private string? _currentCollectionPath;

    public DeckManager() { }

    /// <summary>当前卡组集文件路径；未打开或新建未保存时为 null。</summary>
    public string? CurrentCollectionPath => _currentCollectionPath;

    /// <summary>用于列表/下拉框显示：ID、等级与显示文本「[等级] 卡组名」。</summary>
    public sealed class DeckListItem
    {
        public string Id { get; init; } = "";
        public int Level { get; init; }
        public string Display => $"[{Level}] {Id}";
    }

    /// <summary>新建卡组集：清空当前列表，不关联文件。</summary>
    public void NewCollection()
    {
        _decks.Clear();
        _currentCollectionPath = null;
    }

    /// <summary>打开卡组集：从指定 JSON 文件加载所有卡组，并切换为当前列表。</summary>
    public void OpenCollection(string path)
    {
        var json = File.ReadAllText(path);
        var file = JsonSerializer.Deserialize<DeckCollectionFile>(json, JsonOptions)
            ?? new DeckCollectionFile();
        _decks.Clear();
        foreach (var entry in file.Decks)
        {
            if (string.IsNullOrWhiteSpace(entry.Id)) continue;
            _decks[entry.Id.Trim()] = entry.Deck ?? new Deck();
        }
        _currentCollectionPath = Path.GetFullPath(path);
    }

    /// <summary>保存卡组集到指定路径；若 path 为 null 则保存到当前打开的文件（无路径时返回 false）。</summary>
    public bool SaveCollection(string? path = null)
    {
        var target = path ?? _currentCollectionPath;
        if (string.IsNullOrEmpty(target)) return false;
        target = Path.GetFullPath(target);
        var file = new DeckCollectionFile
        {
            Decks = _decks.Select(kv => new DeckEntry { Id = kv.Key, Deck = kv.Value }).ToList()
        };
        var json = JsonSerializer.Serialize(file, JsonOptions);
        var dir = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(target, json);
        _currentCollectionPath = target;
        return true;
    }

    /// <summary>列出当前卡组集中所有卡组 ID。</summary>
    public IEnumerable<string> List()
    {
        foreach (var id in _decks.Keys)
            yield return id;
    }

    /// <summary>列出卡组并带玩家等级，用于界面显示「[等级] 卡组名」。</summary>
    public IEnumerable<DeckListItem> ListWithLevels()
    {
        foreach (var kv in _decks)
            yield return new DeckListItem { Id = kv.Key, Level = kv.Value.PlayerLevel };
    }

    /// <summary>按 ID 读取卡组，不存在则返回 null。</summary>
    public Deck? Load(string id)
    {
        return _decks.TryGetValue(id, out var deck) ? deck : null;
    }

    /// <summary>保存卡组到当前卡组集（内存）。若提供 resolver 则校验槽位与等级。</summary>
    public void Save(Deck deck, string id, IItemTemplateResolver? resolver = null)
    {
        if (resolver != null)
            Validate(deck, resolver);
        _decks[id] = deck;
    }

    /// <summary>从当前卡组集中删除指定卡组。</summary>
    public void Delete(string id)
    {
        _decks.Remove(id);
    }

    /// <summary>校验槽位总占用、等级与玩家等级限制。</summary>
    public static void Validate(Deck deck, IItemTemplateResolver resolver)
    {
        int maxSlots = Deck.MaxSlotsForLevel(deck.PlayerLevel);
        int totalSlots = 0;
        foreach (var entry in deck.Slots)
        {
            var t = resolver.GetTemplate(entry.ItemName);
            if (t == null)
                throw new ArgumentException($"未知物品：{entry.ItemName}");
            if (!Deck.TierAllowedForLevel(entry.Tier, deck.PlayerLevel))
                throw new ArgumentException($"玩家等级 {deck.PlayerLevel} 不可用 {entry.Tier} 物品：{entry.ItemName}");
            totalSlots += (int)t.Size;
        }
        if (totalSlots > maxSlots)
            throw new ArgumentException($"槽位占用 {totalSlots} 超过上限 {maxSlots}");
    }
}
