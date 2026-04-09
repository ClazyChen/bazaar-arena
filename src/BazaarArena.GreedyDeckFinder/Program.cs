using System.Text;
using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using BazaarArena.ItemDatabase.Mak.Medium;
using BazaarArena.ItemDatabase.Mak.Small;
using BazaarArena.ItemDatabase.Vanessa.Large;
using BazaarArena.ItemDatabase.Vanessa.Medium;
using BazaarArena.ItemDatabase.Vanessa.Small;
using ItemDb = BazaarArena.ItemDatabase.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena.GreedyDeckFinder;

public static class Program
{
    public static int Main(string[] args)
    {
        var config = Config.Parse(args);

        var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
        if (!Directory.Exists(dataDir))
            dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        var levelupsPath = Path.Combine(dataDir, "levelups.json");
        if (File.Exists(levelupsPath))
            LevelUpTable.Load(levelupsPath);

        var db = new ItemDb();
        CommonSmall.RegisterAll(db);
        CommonMedium.RegisterAll(db);
        CommonLarge.RegisterAll(db);
        VanessaSmall.RegisterAll(db);
        VanessaMedium.RegisterAll(db);
        VanessaLarge.RegisterAll(db);
        MakSmall.RegisterAll(db);
        MakMedium.RegisterAll(db);

        foreach (var name in config.SeedOrderedItems)
        {
            if (config.ExcludedItems.Contains(name, StringComparer.Ordinal))
                throw new ArgumentException($"起始卡组中的物品被排除：{name}");
        }
        var pool = new ItemPool(db, config.PlayerLevel, config.PoolHero, config.ExcludedItems);
        IItemTemplateResolver resolver = new GreedyPreflattenedResolver(db, pool, config.PlayerLevel);
        var simulator = new SimulatorClass();
        var rng = config.Seed.HasValue ? new Random(config.Seed.Value) : new Random();
        var perf = new PerfStats();
        var evaluator = new BattleEvaluator(simulator, resolver, rng, config.BestOf, config.Workers, config.PlayerLevel, perf, config.Seed);
        var searcher = new GreedySearcher(config, pool, resolver, evaluator, rng, perf);

        Console.WriteLine($"[GreedyDeckFinder] 玩家等级：{config.PlayerLevel}");
        Console.WriteLine($"[GreedyDeckFinder] 起始卡组（有序）：{string.Join(" | ", config.SeedOrderedItems)}");
        if (config.ExcludedItems.Count > 0)
            Console.WriteLine($"[GreedyDeckFinder] 排除物品：{string.Join("、", config.ExcludedItems)}");
        var topBySize = searcher.Run(config.SeedOrderedItems, (size, top) => PrintSizeResult(size, top));
        if (config.Perf)
            Console.WriteLine(perf.BuildSummary(config.Workers));
        if (!string.IsNullOrWhiteSpace(config.OutputPath))
            WriteResult(config.OutputPath, config.SeedOrderedItems, topBySize);
        return 0;
    }

    private static void PrintSizeResult(int size, List<CandidateState> top)
    {
        Console.WriteLine($"size={size} Top{top.Count}:");
        int rank = 1;
        foreach (var c in top)
        {
            Console.WriteLine($"  {rank}. 分={c.RoundRobinScore:F1} 瑞士={c.SwissScore:F1} 组合={c.ComboKey} 排列=[{string.Join(" | ", c.Representative.ItemNames)}]");
            rank++;
        }
    }

    private static void WriteResult(string path, IReadOnlyList<string> seedOrdered, Dictionary<int, List<CandidateState>> topBySize)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"起始卡组（有序）: {string.Join(" | ", seedOrdered)}");
        foreach (var s in topBySize.Keys.OrderBy(x => x))
        {
            sb.AppendLine($"size={s}");
            int rank = 1;
            foreach (var c in topBySize[s])
            {
                sb.AppendLine($"  {rank}. RR={c.RoundRobinScore:F1}, Swiss={c.SwissScore:F1}, Combo={c.ComboKey}, Rep=[{string.Join(" | ", c.Representative.ItemNames)}]");
                rank++;
            }
            sb.AppendLine();
        }
        File.WriteAllText(path, sb.ToString());
        Console.WriteLine($"结果已写入: {path}");
    }
}
