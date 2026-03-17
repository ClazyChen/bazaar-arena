using System.Text.Json;

namespace BazaarArena.QualityDeckFinder;

/// <summary>命令行与运行配置。</summary>
public sealed class Config
{
    /// <summary>可选：从 JSON 读取配置（--config）。</summary>
    public string? ConfigPath { get; set; }

    public string? ResumePath { get; set; }
    public string StatePath { get; set; } = "quality_deck_state.json";
    public int TopInterval { get; set; } = 10;
    public int SaveInterval { get; set; } = 20;
    public int SegmentCap { get; set; } = 50;
    public int GamesPerEval { get; set; } = 5;
    public int MaxClimbSteps { get; set; } = 500;
    public int RestartsPerShape { get; set; } = 5;
    public int NeighborSampleSize { get; set; } = 80;
    public int MabBudgetPerStep { get; set; } = 30;
    public double InitialElo { get; set; } = 1500;
    public double EloK { get; set; } = 32;

    /// <summary>内战每次比较的对局数（更快探索建议 3~5）。</summary>
    public int InnerWars { get; set; } = 3;
    /// <summary>组合内部筛选预算：尝试多少次随机交换候选。</summary>
    public int InnerBudget { get; set; } = 30;
    /// <summary>内战筛选后保留的候选数（Top-K）。</summary>
    public int InnerSelectTop { get; set; } = 3;
    /// <summary>候选排序时每个 match 的对局数。</summary>
    public int InnerSelectWars { get; set; } = 2;

    /// <summary>外战确认：抽取多少个强对手签名（来自所在段及更低段）。</summary>
    public int ConfirmOpponents { get; set; } = 8;
    /// <summary>外战确认：每个对手对局数。</summary>
    public int ConfirmGamesPerOpponent { get; set; } = 1;

    /// <summary>探索混合比例：1=完全均匀探索，0=完全按先验加权。</summary>
    public double ExploreMix { get; set; } = 0.30;
    /// <summary>先验 EMA 平滑系数。</summary>
    public double PriorEmaAlpha { get; set; } = 0.08;

    /// <summary>分段边界（ELO）：段0 [0,bounds[0]), 段1 [bounds[0],bounds[1]), …；运行中可向高分方向追加。读写须在 SegmentBoundsLock 内进行。</summary>
    public List<double> SegmentBounds { get; set; } = [1400, 1600, 1800];

    /// <summary>分段边界的读写锁，扩展与读取（SegmentIndex、SignaturesInSegment、Save）均须使用。</summary>
    public object SegmentBoundsLock { get; } = new object();

    /// <summary>分段自动扩展步长：当池内最高 ELO 超过当前最高段下界超过此值时追加新边界。</summary>
    public double SegmentExpandStep { get; set; } = 200;
    /// <summary>分段边界数量上限（对应段数=上限+1），防止无限扩展。</summary>
    public int SegmentExpandMaxBounds { get; set; } = 10;

    /// <summary>每完成 n 次爬山执行一次随机卡组注入；0 表示禁用。</summary>
    public int InjectInterval { get; set; } = 20;
    /// <summary>每次注入最多尝试加入的随机卡组数量。</summary>
    public int InjectCount { get; set; } = 1;

    /// <summary>并行 worker 数量；0 表示仅主线程运行（不启用多 worker）。</summary>
    public int Workers { get; set; } = 0;

    public static Config Parse(string[] args)
    {
        // 先找 --config，若存在则从 JSON 读取作为基准配置
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
        c.SegmentBounds ??= [1400, 1600, 1800];
        if (c.SegmentBounds.Count == 0) c.SegmentBounds = [1400, 1600, 1800];

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config" when i + 1 < args.Length:
                    // 已在上面加载；这里仅跳过参数值
                    i++;
                    break;
                case "--resume" when i + 1 < args.Length:
                    c.ResumePath = args[++i];
                    break;
                case "--state" when i + 1 < args.Length:
                    c.StatePath = args[++i];
                    break;
                case "--top-interval" when i + 1 < args.Length && int.TryParse(args[i + 1], out var ti):
                    c.TopInterval = ti;
                    i++;
                    break;
                case "--save-interval" when i + 1 < args.Length && int.TryParse(args[i + 1], out var si):
                    c.SaveInterval = si;
                    i++;
                    break;
                case "--segment-cap" when i + 1 < args.Length && int.TryParse(args[i + 1], out var sc):
                    c.SegmentCap = sc;
                    i++;
                    break;
                case "--games-per-eval" when i + 1 < args.Length && int.TryParse(args[i + 1], out var gpe):
                    c.GamesPerEval = gpe;
                    i++;
                    break;
                case "--max-climb-steps" when i + 1 < args.Length && int.TryParse(args[i + 1], out var mcs):
                    c.MaxClimbSteps = mcs;
                    i++;
                    break;
                case "--restarts-per-shape" when i + 1 < args.Length && int.TryParse(args[i + 1], out var rps):
                    c.RestartsPerShape = rps;
                    i++;
                    break;
                case "--neighbor-sample" when i + 1 < args.Length && int.TryParse(args[i + 1], out var ns):
                    c.NeighborSampleSize = ns;
                    i++;
                    break;
                case "--mab-budget" when i + 1 < args.Length && int.TryParse(args[i + 1], out var mb):
                    c.MabBudgetPerStep = mb;
                    i++;
                    break;
                case "--segment-expand-step" when i + 1 < args.Length && double.TryParse(args[i + 1], out var ses):
                    c.SegmentExpandStep = ses;
                    i++;
                    break;
                case "--segment-expand-max-bounds" when i + 1 < args.Length && int.TryParse(args[i + 1], out var semb):
                    c.SegmentExpandMaxBounds = semb;
                    i++;
                    break;
                case "--inject-interval" when i + 1 < args.Length && int.TryParse(args[i + 1], out var ii):
                    c.InjectInterval = ii;
                    i++;
                    break;
                case "--inject-count" when i + 1 < args.Length && int.TryParse(args[i + 1], out var ic):
                    c.InjectCount = ic;
                    i++;
                    break;
                case "--workers" when i + 1 < args.Length && int.TryParse(args[i + 1], out var w) && w >= 0:
                    c.Workers = w;
                    i++;
                    break;
                case "--inner-wars" when i + 1 < args.Length && int.TryParse(args[i + 1], out var iw):
                    c.InnerWars = iw;
                    i++;
                    break;
                case "--inner-budget" when i + 1 < args.Length && int.TryParse(args[i + 1], out var ib):
                    c.InnerBudget = ib;
                    i++;
                    break;
                case "--inner-select-top" when i + 1 < args.Length && int.TryParse(args[i + 1], out var ist):
                    c.InnerSelectTop = ist;
                    i++;
                    break;
                case "--inner-select-wars" when i + 1 < args.Length && int.TryParse(args[i + 1], out var isw):
                    c.InnerSelectWars = isw;
                    i++;
                    break;
                case "--confirm-opponents" when i + 1 < args.Length && int.TryParse(args[i + 1], out var co):
                    c.ConfirmOpponents = co;
                    i++;
                    break;
                case "--confirm-games" when i + 1 < args.Length && int.TryParse(args[i + 1], out var cg):
                    c.ConfirmGamesPerOpponent = cg;
                    i++;
                    break;
                case "--explore-mix" when i + 1 < args.Length && double.TryParse(args[i + 1], out var em):
                    c.ExploreMix = em;
                    i++;
                    break;
                case "--prior-ema" when i + 1 < args.Length && double.TryParse(args[i + 1], out var pe):
                    c.PriorEmaAlpha = pe;
                    i++;
                    break;
            }
        }
        return c;
    }

    public static Config LoadFromJson(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"配置文件不存在：{path}", path);

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        var cfg = JsonSerializer.Deserialize<Config>(json, options) ?? new Config();

        // 修正可能缺失的默认值（避免用户 JSON 只写一部分时得到 0/空）
        cfg.StatePath ??= "quality_deck_state.json";
        cfg.SegmentBounds ??= [1400, 1600, 1800];
        if (cfg.SegmentBounds.Count == 0) cfg.SegmentBounds = [1400, 1600, 1800];
        if (cfg.TopInterval <= 0) cfg.TopInterval = 10;
        if (cfg.SaveInterval <= 0) cfg.SaveInterval = 20;
        if (cfg.SegmentCap <= 0) cfg.SegmentCap = 50;
        if (cfg.GamesPerEval <= 0) cfg.GamesPerEval = 5;
        if (cfg.MaxClimbSteps <= 0) cfg.MaxClimbSteps = 500;
        if (cfg.NeighborSampleSize <= 0) cfg.NeighborSampleSize = 80;
        if (cfg.MabBudgetPerStep <= 0) cfg.MabBudgetPerStep = 30;
        if (cfg.InitialElo <= 0) cfg.InitialElo = 1500;
        if (cfg.EloK <= 0) cfg.EloK = 32;
        if (cfg.InnerWars <= 0) cfg.InnerWars = 3;
        if (cfg.InnerBudget <= 0) cfg.InnerBudget = 30;
        if (cfg.InnerSelectTop <= 0) cfg.InnerSelectTop = 3;
        if (cfg.InnerSelectWars <= 0) cfg.InnerSelectWars = 2;
        if (cfg.ConfirmOpponents < 0) cfg.ConfirmOpponents = 8;
        if (cfg.ConfirmGamesPerOpponent <= 0) cfg.ConfirmGamesPerOpponent = 1;
        cfg.ExploreMix = Math.Clamp(cfg.ExploreMix, 0.0, 1.0);
        cfg.PriorEmaAlpha = Math.Clamp(cfg.PriorEmaAlpha, 0.0, 1.0);
        if (cfg.SegmentExpandStep <= 0) cfg.SegmentExpandStep = 200;
        if (cfg.SegmentExpandMaxBounds <= 0) cfg.SegmentExpandMaxBounds = 10;
        if (cfg.InjectInterval < 0) cfg.InjectInterval = 20;
        if (cfg.InjectCount < 0) cfg.InjectCount = 1;
        if (cfg.Workers < 0) cfg.Workers = 0;

        return cfg;
    }
}
