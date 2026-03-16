namespace BazaarArena.QualityDeckFinder;

/// <summary>命令行与运行配置。</summary>
public sealed class Config
{
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

    /// <summary>分段边界（ELO）：段0 [0,1400), 段1 [1400,1600), 段2 [1600,1800), 段3 [1800,+∞)。</summary>
    public IReadOnlyList<double> SegmentBounds { get; set; } = [1400, 1600, 1800];

    public static Config Parse(string[] args)
    {
        var c = new Config();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
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
            }
        }
        return c;
    }
}
