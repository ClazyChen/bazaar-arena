using System.Text.Json;

namespace BazaarArena.GreedyDeckFinder;

/// <summary>锚定贪心搜索器配置。</summary>
public sealed class Config
{
    public string? ConfigPath { get; set; }
    public string AnchorItem { get; set; } = "";

    /// <summary>有序起始卡组（与 <see cref="AnchorItem"/> 二选一互斥；最终运行以此列表为准，单锚点时由 Parse 填入单元素）。</summary>
    public List<string> SeedOrderedItems { get; set; } = [];

    public int TopK { get; set; } = 10;
    public int TopMultiplier { get; set; } = 3;
    public int BestOf { get; set; } = 5;
    public int? Seed { get; set; }
    public int Workers { get; set; } = 0;
    public string? OutputPath { get; set; }
    public bool Perf { get; set; } = false;
    public List<string> ExcludedItems { get; set; } = [];

    /// <summary>玩家等级（仅支持 2、3、4、5 或 6）：影响槽位上限、对战 Deck.PlayerLevel、overridable 预应用尺度。</summary>
    public int PlayerLevel { get; set; } = 2;

    public static Config Parse(string[] args)
    {
        string? configPath = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config" && i + 1 < args.Length)
            {
                configPath = args[i + 1];
                break;
            }
        }

        var c = configPath != null ? LoadFromJson(configPath) : new Config();
        c.ConfigPath = configPath;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config" when i + 1 < args.Length:
                    i++;
                    break;
                case "--anchor-item" when i + 1 < args.Length:
                    c.AnchorItem = args[++i];
                    break;
                case "--seed-items" when i + 1 < args.Length:
                {
                    foreach (var name in SplitCsv(args[++i]))
                        c.SeedOrderedItems.Add(name);
                    break;
                }
                case "--top-k" when i + 1 < args.Length && int.TryParse(args[i + 1], out var k):
                    c.TopK = k;
                    i++;
                    break;
                case "--top-multiplier" when i + 1 < args.Length && int.TryParse(args[i + 1], out var m):
                    c.TopMultiplier = m;
                    i++;
                    break;
                case "--bo" when i + 1 < args.Length && int.TryParse(args[i + 1], out var bo):
                    c.BestOf = bo;
                    i++;
                    break;
                case "--seed" when i + 1 < args.Length && int.TryParse(args[i + 1], out var seed):
                    c.Seed = seed;
                    i++;
                    break;
                case "--workers" when i + 1 < args.Length && int.TryParse(args[i + 1], out var workers):
                    c.Workers = workers;
                    i++;
                    break;
                case "--output" when i + 1 < args.Length:
                    c.OutputPath = args[++i];
                    break;
                case "--perf":
                    c.Perf = true;
                    break;
                case "--exclude-item" when i + 1 < args.Length:
                {
                    foreach (var name in SplitCsv(args[++i]))
                    {
                        if (!c.ExcludedItems.Contains(name, StringComparer.Ordinal))
                            c.ExcludedItems.Add(name);
                    }
                    break;
                }
                case "--level":
                {
                    if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out var lvl))
                        throw new ArgumentException("--level 需要整数参数");
                    c.PlayerLevel = lvl;
                    i++;
                    break;
                }
            }
        }

        if (c.PlayerLevel != 2 && c.PlayerLevel != 3 && c.PlayerLevel != 4 && c.PlayerLevel != 5 && c.PlayerLevel != 6)
            throw new ArgumentException("仅支持 --level 2、3、4、5 或 6。");

        c.SeedOrderedItems = c.SeedOrderedItems
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();
        c.AnchorItem = (c.AnchorItem ?? string.Empty).Trim();

        bool hasSeedList = c.SeedOrderedItems.Count > 0;
        bool hasAnchor = c.AnchorItem.Length > 0;
        if (hasSeedList && hasAnchor)
            throw new ArgumentException("请勿同时指定 --anchor-item 与 --seed-items。");
        if (!hasSeedList && !hasAnchor)
            throw new ArgumentException("必须提供 --anchor-item 或 --seed-items。");
        if (!hasSeedList)
            c.SeedOrderedItems = [c.AnchorItem];

        if (c.TopK <= 0) c.TopK = 10;
        if (c.TopMultiplier <= 0) c.TopMultiplier = 3;
        if (c.BestOf <= 0 || c.BestOf % 2 == 0) c.BestOf = 5;
        if (c.Workers < 0) c.Workers = 0;
        c.ExcludedItems = c.ExcludedItems
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return c;
    }

    private static IEnumerable<string> SplitCsv(string input)
    {
        return (input ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static Config LoadFromJson(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"配置文件不存在：{path}", path);

        var text = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        var cfg = JsonSerializer.Deserialize<Config>(text, options) ?? new Config();
        // System.Text.Json 省略 int 属性时为 0，与默认 2 级对齐
        if (cfg.PlayerLevel == 0)
            cfg.PlayerLevel = 2;
        return cfg;
    }
}
