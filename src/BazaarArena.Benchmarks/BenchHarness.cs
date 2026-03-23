using System.Text.Json;
using BazaarArena.Core;
using BazaarArena.DeckManager;
using BazaarArena.ItemDatabase;
using BazaarArena.ItemDatabase.Vanessa.Large;
using BazaarArena.ItemDatabase.Vanessa.Medium;
using BazaarArena.ItemDatabase.Vanessa.Small;
using ItemDb = BazaarArena.ItemDatabase.ItemDatabase;

namespace BazaarArena.Benchmarks;

/// <summary>加载 levelups、物品库与卡组 JSON，供基准共用。</summary>
internal static class BenchHarness
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    internal static void LoadLevelUps()
    {
        var path = ResolveDataPath("levelups.json");
        if (File.Exists(path))
            LevelUpTable.Load(path);
    }

    internal static ItemDb CreateItemDatabase()
    {
        var db = new ItemDb();
        CommonSmall.RegisterAll(db);
        CommonMedium.RegisterAll(db);
        CommonLarge.RegisterAll(db);
        VanessaSmall.RegisterAll(db);
        VanessaMedium.RegisterAll(db);
        VanessaLarge.RegisterAll(db);
        return db;
    }

    internal static Deck LoadDeck(string collectionRelativePath, string deckId)
    {
        var path = ResolveDataPath(collectionRelativePath);
        var json = File.ReadAllText(path);
        var file = JsonSerializer.Deserialize<DeckCollectionFile>(json, JsonOptions)
            ?? throw new InvalidOperationException($"无法解析卡组集: {path}");
        var entry = file.Decks.Find(e => string.Equals(e.Id, deckId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"卡组集中无 ID: {deckId}（{path}）");
        return entry.Deck;
    }

    /// <summary>从输出目录或仓库根目录解析 Data 下相对路径。</summary>
    internal static string ResolveDataPath(string relativeUnderData)
    {
        var name = relativeUnderData.Replace('/', Path.DirectorySeparatorChar);
        var inOutput = Path.Combine(AppContext.BaseDirectory, "Data", name);
        if (File.Exists(inOutput))
            return inOutput;
        var cwd = Path.Combine(Directory.GetCurrentDirectory(), "Data", name);
        if (File.Exists(cwd))
            return cwd;
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Data", name));
        if (File.Exists(repo))
            return repo;
        throw new FileNotFoundException($"找不到数据文件: Data/{relativeUnderData}（已试 BaseDirectory、当前目录、仓库 Data）");
    }
}
