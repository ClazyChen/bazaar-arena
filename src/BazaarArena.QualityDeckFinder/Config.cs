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

    /// <summary>物品对协同加权系数（0 表示不使用物品对协同）。</summary>
    public double SynergyPairLambda { get; set; } = 0.35;

    /// <summary>Priors 学习信号裁剪：|signal| 最大值（signal=elo-baseline）。</summary>
    public double PriorsSignalClip { get; set; } = 200;

    /// <summary>未确认样本更新 Priors 的倍率（0~1）：越小越稳、学习越慢。</summary>
    public double PriorsUnconfirmedMultiplier { get; set; } = 0.25;

    /// <summary>达到该对局数后视为“可信度满”，不再按对局数降权。</summary>
    public int PriorsMinGamesForFullWeight { get; set; } = 30;

    /// <summary>Priors 退火尺度：总对局数达到该值后从“机制主导”逐步过渡到“组合主导”。</summary>
    public int PriorsAnnealGames { get; set; } = 5000;

    /// <summary>候选生成：最低随机比例（防止塌缩）。</summary>
    public double CandidateRandomMixMin { get; set; } = 0.15;

    /// <summary>候选生成：单物品强度模式比例（早期）。</summary>
    public double CandidateItemOnlyMixStart { get; set; } = 0.60;

    /// <summary>候选生成：单物品强度模式比例（后期）。</summary>
    public double CandidateItemOnlyMixEnd { get; set; } = 0.15;

    /// <summary>anchored（固定物品）搜索占比：每次爬山前以该概率选择一个锚点物品，并强制卡组包含它。</summary>
    public double AnchoredMix { get; set; } = 0.50;

    /// <summary>报告中输出多少个物品的“最强拍档卡组”。</summary>
    public int AnchoredReportCount { get; set; } = 12;

    /// <summary>锚定代表选择：softmax 温度（ELO 差/温度），越大越均匀。</summary>
    public double RepresentativeTemperature { get; set; } = 100;

    /// <summary>锚定代表选择：探索概率（以该概率从该物品的锚定玩家中均匀抽一名）。</summary>
    public double RepresentativeExploreProb { get; set; } = 0.10;

    /// <summary>锚定代表选择：最小对局数低于此值的锚定玩家权重降权（0 表示不降权）。</summary>
    public int MinGamesForRepresentative { get; set; } = 0;

    /// <summary>分段边界（ELO）：段0 [0,bounds[0]), 段1 [bounds[0],bounds[1]), …；固定五段，仅从配置/状态加载。读写须在 SegmentBoundsLock 内进行。</summary>
    public List<double> SegmentBounds { get; set; } = [1600, 1800, 2000, 2200];

    /// <summary>分段边界的读写锁，读取（SegmentIndex、SignaturesInSegment、Save）均须使用。</summary>
    public object SegmentBoundsLock { get; } = new object();

    /// <summary>单赛季每玩家最大对局数（匹配赛阶段）。</summary>
    public int SeasonMatchCap { get; set; } = 30;
    /// <summary>单赛季每玩家最大失败次数，超过则提前停止该玩家本季匹配。</summary>
    public int SeasonLossCap { get; set; } = 10;

    /// <summary>每完成 n 个赛季执行一次随机强度玩家注入；0 表示禁用。</summary>
    public int InjectInterval { get; set; } = 20;
    /// <summary>每次注入最多尝试加入的随机强度玩家数量。</summary>
    public int InjectCount { get; set; } = 1;

    /// <summary>连续多少赛季无改进则放弃：强度玩家移出列表（卡组留池），锚定玩家用含该物品的随机卡组重启。0 表示不放弃。</summary>
    public int AbandonSeasonsThreshold { get; set; } = 15;

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
        c.SegmentBounds ??= [1600, 1800, 2000, 2200];
        if (c.SegmentBounds.Count == 0) c.SegmentBounds = [1600, 1800, 2000, 2200];

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
                case "--synergy-pair" when i + 1 < args.Length && double.TryParse(args[i + 1], out var spl):
                    c.SynergyPairLambda = spl;
                    i++;
                    break;
                case "--priors-clip" when i + 1 < args.Length && double.TryParse(args[i + 1], out var pc):
                    c.PriorsSignalClip = pc;
                    i++;
                    break;
                case "--priors-unconfirmed" when i + 1 < args.Length && double.TryParse(args[i + 1], out var pum):
                    c.PriorsUnconfirmedMultiplier = pum;
                    i++;
                    break;
                case "--priors-full-games" when i + 1 < args.Length && int.TryParse(args[i + 1], out var pfg):
                    c.PriorsMinGamesForFullWeight = pfg;
                    i++;
                    break;
                case "--priors-anneal-games" when i + 1 < args.Length && int.TryParse(args[i + 1], out var pag):
                    c.PriorsAnnealGames = pag;
                    i++;
                    break;
                case "--cand-rand-min" when i + 1 < args.Length && double.TryParse(args[i + 1], out var crm):
                    c.CandidateRandomMixMin = crm;
                    i++;
                    break;
                case "--cand-item-start" when i + 1 < args.Length && double.TryParse(args[i + 1], out var cis):
                    c.CandidateItemOnlyMixStart = cis;
                    i++;
                    break;
                case "--cand-item-end" when i + 1 < args.Length && double.TryParse(args[i + 1], out var cie):
                    c.CandidateItemOnlyMixEnd = cie;
                    i++;
                    break;
                case "--anchored-mix" when i + 1 < args.Length && double.TryParse(args[i + 1], out var am):
                    c.AnchoredMix = am;
                    i++;
                    break;
                case "--anchored-report" when i + 1 < args.Length && int.TryParse(args[i + 1], out var arc):
                    c.AnchoredReportCount = arc;
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
        cfg.SegmentBounds ??= [1600, 1800, 2000, 2200];
        if (cfg.SegmentBounds.Count == 0) cfg.SegmentBounds = [1600, 1800, 2000, 2200];
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
        cfg.SynergyPairLambda = Math.Max(0, cfg.SynergyPairLambda);
        cfg.PriorsSignalClip = Math.Max(1.0, cfg.PriorsSignalClip);
        cfg.PriorsUnconfirmedMultiplier = Math.Clamp(cfg.PriorsUnconfirmedMultiplier, 0.0, 1.0);
        if (cfg.PriorsMinGamesForFullWeight <= 0) cfg.PriorsMinGamesForFullWeight = 30;
        if (cfg.PriorsAnnealGames <= 0) cfg.PriorsAnnealGames = 5000;
        cfg.CandidateRandomMixMin = Math.Clamp(cfg.CandidateRandomMixMin, 0.0, 1.0);
        cfg.CandidateItemOnlyMixStart = Math.Clamp(cfg.CandidateItemOnlyMixStart, 0.0, 1.0);
        cfg.CandidateItemOnlyMixEnd = Math.Clamp(cfg.CandidateItemOnlyMixEnd, 0.0, 1.0);
        cfg.AnchoredMix = Math.Clamp(cfg.AnchoredMix, 0.0, 1.0);
        if (cfg.AnchoredReportCount < 0) cfg.AnchoredReportCount = 12;
        if (cfg.InjectInterval < 0) cfg.InjectInterval = 20;
        if (cfg.InjectCount < 0) cfg.InjectCount = 1;
        if (cfg.AbandonSeasonsThreshold < 0) cfg.AbandonSeasonsThreshold = 15;
        if (cfg.Workers < 0) cfg.Workers = 0;

        return cfg;
    }
}
