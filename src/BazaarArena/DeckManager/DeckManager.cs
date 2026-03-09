using System.Text.Json;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;

namespace BazaarArena.DeckManager;

/// <summary>卡组管理器：CRUD，以 JSON 形式保存在本地目录。</summary>
public class DeckManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _baseDir;

    public DeckManager(string? baseDir = null)
    {
        _baseDir = baseDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BazaarArena", "Decks");
        Directory.CreateDirectory(_baseDir);
    }

    /// <summary>用于列表/下拉框显示：ID、等级与显示文本「[等级] 卡组名」。</summary>
    public sealed class DeckListItem
    {
        public string Id { get; init; } = "";
        public int Level { get; init; }
        public string Display => $"[{Level}] {Id}";
    }

    /// <summary>列出已保存的卡组 ID（文件名无扩展名）。</summary>
    public IEnumerable<string> List()
    {
        foreach (var f in Directory.EnumerateFiles(_baseDir, "*.json"))
            yield return Path.GetFileNameWithoutExtension(f);
    }

    /// <summary>列出卡组并带玩家等级，用于界面显示「[等级] 卡组名」。</summary>
    public IEnumerable<DeckListItem> ListWithLevels()
    {
        foreach (var id in List())
        {
            var deck = Load(id);
            yield return new DeckListItem { Id = id, Level = deck?.PlayerLevel ?? 1 };
        }
    }

    /// <summary>按 ID 读取卡组，不存在则返回 null。</summary>
    public Deck? Load(string id)
    {
        var path = GetPath(id);
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Deck>(json, JsonOptions);
    }

    /// <summary>保存卡组。若提供 resolver 则校验槽位与等级。</summary>
    public void Save(Deck deck, string id, IItemTemplateResolver? resolver = null)
    {
        if (resolver != null)
            Validate(deck, resolver);

        var path = GetPath(id);
        var json = JsonSerializer.Serialize(deck, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>删除指定卡组。</summary>
    public void Delete(string id)
    {
        var path = GetPath(id);
        if (File.Exists(path))
            File.Delete(path);
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

    private string GetPath(string id)
    {
        var safe = string.Join("_", id.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_baseDir, safe + ".json");
    }
}
