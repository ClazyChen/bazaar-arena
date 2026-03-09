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
    /// <summary>卡组 ID 顺序，与 JSON 中 decks 数组顺序一致。</summary>
    private readonly List<string> _order = [];
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
        _order.Clear();
        _currentCollectionPath = null;
    }

    /// <summary>打开卡组集：从指定 JSON 文件加载所有卡组，并切换为当前列表。</summary>
    public void OpenCollection(string path)
    {
        var json = File.ReadAllText(path);
        var file = JsonSerializer.Deserialize<DeckCollectionFile>(json, JsonOptions)
            ?? new DeckCollectionFile();
        _decks.Clear();
        _order.Clear();
        foreach (var entry in file.Decks)
        {
            if (string.IsNullOrWhiteSpace(entry.Id)) continue;
            var id = entry.Id.Trim();
            _order.Add(id);
            _decks[id] = entry.Deck ?? new Deck();
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
            Decks = _order.Select(id => new DeckEntry { Id = id, Deck = _decks[id] }).ToList()
        };
        var json = JsonSerializer.Serialize(file, JsonOptions);
        var dir = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(target, json);
        _currentCollectionPath = target;
        return true;
    }

    /// <summary>列出当前卡组集中所有卡组 ID（顺序与 JSON 一致）。</summary>
    public IEnumerable<string> List()
    {
        foreach (var id in _order)
            yield return id;
    }

    /// <summary>列出卡组并带玩家等级，用于界面显示「[等级] 卡组名」；顺序与 JSON 一致。</summary>
    public IEnumerable<DeckListItem> ListWithLevels()
    {
        foreach (var id in _order)
        {
            if (_decks.TryGetValue(id, out var deck))
                yield return new DeckListItem { Id = id, Level = deck.PlayerLevel };
        }
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
        if (!_order.Contains(id))
            _order.Add(id);
    }

    /// <summary>从当前卡组集中删除指定卡组。</summary>
    public void Delete(string id)
    {
        _decks.Remove(id);
        _order.Remove(id);
    }

    /// <summary>重命名卡组；若新名称已存在则抛出 <see cref="ArgumentException"/>。</summary>
    public void Rename(string oldId, string newId)
    {
        if (string.IsNullOrWhiteSpace(newId))
            throw new ArgumentException("卡组名称不能为空。", nameof(newId));
        newId = newId.Trim();
        if (oldId == newId) return;
        if (!_decks.TryGetValue(oldId, out var deck))
            throw new ArgumentException($"卡组不存在：{oldId}", nameof(oldId));
        if (_decks.ContainsKey(newId))
            throw new ArgumentException($"已存在同名卡组：{newId}", nameof(newId));
        _decks.Remove(oldId);
        _decks[newId] = deck;
        var idx = _order.IndexOf(oldId);
        if (idx >= 0)
            _order[idx] = newId;
    }

    /// <summary>将卡组上移一位；已在首位则无效果。返回是否发生了移动。</summary>
    public bool MoveUp(string id)
    {
        var i = _order.IndexOf(id);
        if (i <= 0) return false;
        (_order[i - 1], _order[i]) = (_order[i], _order[i - 1]);
        return true;
    }

    /// <summary>将卡组下移一位；已在末位则无效果。返回是否发生了移动。</summary>
    public bool MoveDown(string id)
    {
        var i = _order.IndexOf(id);
        if (i < 0 || i >= _order.Count - 1) return false;
        (_order[i], _order[i + 1]) = (_order[i + 1], _order[i]);
        return true;
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
