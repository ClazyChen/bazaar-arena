using System.Text.Json;

namespace BazaarArena.Core;

/// <summary>等级到最大生命值映射：1 级为 300，2 级 = 300 + Level1 的 HealthIncrease，L 级 = 300 + Level1～Level(L-1) 的 HealthIncrease 之和。</summary>
public static class LevelUpTable
{
    /// <summary>1 级时的最大生命值。</summary>
    public const int BaseMaxHpLevel1 = 300;

    private static Dictionary<int, int>? _levelToMaxHp;
    private static readonly object _lock = new();

    /// <summary>从 JSON 文件加载并构建等级→最大生命值表。JSON 根为版本键（如 "5.0.0"），值为 LevelUpEntry 数组。</summary>
    public static void Load(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        JsonElement arrayElement = default;
        foreach (var prop in root.EnumerateObject())
        {
            arrayElement = prop.Value;
            break;
        }
        List<LevelUpEntry> list = [];
        foreach (var item in arrayElement.EnumerateArray())
        {
            list.Add(new LevelUpEntry
            {
                Level = item.GetProperty("Level").GetInt32(),
                HealthIncrease = item.GetProperty("HealthIncrease").GetInt32(),
            });
        }
        var levelToIncrease = list.ToDictionary(e => e.Level, e => e.HealthIncrease);
        var levelToMaxHp = new Dictionary<int, int> { [1] = BaseMaxHpLevel1 };
        int cumulative = BaseMaxHpLevel1;
        int maxLevel = levelToIncrease.Keys.Max();
        for (int level = 2; level <= maxLevel; level++)
        {
            // L 级最大生命值 = 300 + Level1 的 HealthIncrease + … + Level(L-1) 的 HealthIncrease
            if (levelToIncrease.TryGetValue(level - 1, out int inc))
                cumulative += inc;
            levelToMaxHp[level] = cumulative;
        }
        lock (_lock)
        {
            _levelToMaxHp = levelToMaxHp;
        }
    }

    /// <summary>获取指定等级的最大生命值。若未加载或等级超出范围，则回退为 300 + (level-1)*100 的简单公式。</summary>
    public static int GetMaxHp(int level)
    {
        if (level < 1) level = 1;
        var table = _levelToMaxHp;
        if (table != null && table.TryGetValue(level, out int hp))
            return hp;
        return BaseMaxHpLevel1 + (level - 1) * 100;
    }

    /// <summary>是否已加载数据。</summary>
    public static bool IsLoaded => _levelToMaxHp != null;
}
