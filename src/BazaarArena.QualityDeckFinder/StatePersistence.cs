using System.Text.Encodings.Web;
using System.Text.Json;
using BazaarArena.ItemDatabase;
using ItemDb = BazaarArena.ItemDatabase.ItemDatabase;

namespace BazaarArena.QualityDeckFinder;

public static class StatePersistence
{
    private const int Version = 5;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Save(string path, OptimizerState state)
    {
        List<double> boundsSnapshot;
        lock (state.Config.SegmentBoundsLock)
        {
            boundsSnapshot = state.Config.SegmentBounds.ToList();
        }

        var poolValues = state.Pool.Values.ToList();
        int poolSize = poolValues.Count;
        int confirmedCount = poolValues.Count(e => e.IsConfirmed);
        int localOptimaCount = poolValues.Count(e => e.IsLocalOptimum);
        double maxElo = poolSize > 0 ? poolValues.Max(e => e.Elo) : state.Config.InitialElo;
        double minElo = poolSize > 0 ? poolValues.Min(e => e.Elo) : state.Config.InitialElo;
        var dto = new StateDto
        {
            Version = Version,
            SegmentBounds = boundsSnapshot,
            SegmentCap = state.Config.SegmentCap,
            TotalRestarts = state.TotalRestarts,
            TotalClimbs = state.TotalClimbs,
            TotalGames = state.TotalGames,
            RngSeed = state.RngSeed,
            Summary = new SummaryDto
            {
                PoolSize = poolSize,
                ConfirmedCount = confirmedCount,
                LocalOptimaCount = localOptimaCount,
                MaxElo = maxElo,
                MinElo = minElo,
            },
            ConfigSnapshot = new ConfigSnapshotDto
            {
                TopInterval = state.Config.TopInterval,
                SaveInterval = state.Config.SaveInterval,
                SegmentCap = state.Config.SegmentCap,
                InitialElo = state.Config.InitialElo,
                EloK = state.Config.EloK,
                GamesPerEval = state.Config.GamesPerEval,
                MaxClimbSteps = state.Config.MaxClimbSteps,
                NeighborSampleSize = state.Config.NeighborSampleSize,
                MabBudgetPerStep = state.Config.MabBudgetPerStep,
                InnerWars = state.Config.InnerWars,
                InnerBudget = state.Config.InnerBudget,
                InnerSelectTop = state.Config.InnerSelectTop,
                InnerSelectWars = state.Config.InnerSelectWars,
                ConfirmOpponents = state.Config.ConfirmOpponents,
                ConfirmGamesPerOpponent = state.Config.ConfirmGamesPerOpponent,
                ExploreMix = state.Config.ExploreMix,
                PriorEmaAlpha = state.Config.PriorEmaAlpha,
                SynergyPairLambda = state.Config.SynergyPairLambda,
                SynergyMechanicLambda = state.Config.SynergyMechanicLambda,
                AnchoredMix = state.Config.AnchoredMix,
                AnchoredReportCount = state.Config.AnchoredReportCount,
                InjectInterval = state.Config.InjectInterval,
                InjectCount = state.Config.InjectCount,
                Workers = state.Config.Workers,
                FastLaneEnabled = state.Config.FastLaneEnabled,
                FastLaneEloDeltaThreshold = state.Config.FastLaneEloDeltaThreshold,
                PriorsSignalClip = state.Config.PriorsSignalClip,
                PriorsUnconfirmedMultiplier = state.Config.PriorsUnconfirmedMultiplier,
                PriorsMinGamesForFullWeight = state.Config.PriorsMinGamesForFullWeight,
                PriorsAnnealGames = state.Config.PriorsAnnealGames,
                CandidateRandomMixMin = state.Config.CandidateRandomMixMin,
                CandidateItemOnlyMixStart = state.Config.CandidateItemOnlyMixStart,
                CandidateItemOnlyMixEnd = state.Config.CandidateItemOnlyMixEnd,
                RerateIntervalClimbs = state.Config.RerateIntervalClimbs,
                RerateBatchSize = state.Config.RerateBatchSize,
                RerateGamesPerDeck = state.Config.RerateGamesPerDeck,
            },
            Priors = new PriorsDto
            {
                EmaAlpha = state.Priors.EmaAlpha,
                ShapeCountWeights = state.Priors.ShapeCountWeights.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
                ItemWeights = state.Priors.ItemWeights.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
                PairWeights = state.Priors.PairWeights.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
                MechanicPairWeights = state.Priors.MechanicPairWeights.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
            },
            Combos = state.Pool.Select(kv => new ComboDto
            {
                ComboSig = kv.Key,
                RepresentativeShape = kv.Value.Representative.Shape.ToList(),
                RepresentativeItems = kv.Value.Representative.ItemNames.ToList(),
                Elo = kv.Value.Elo,
                IsLocalOptimum = kv.Value.IsLocalOptimum,
                IsConfirmed = kv.Value.IsConfirmed,
                GameCount = kv.Value.GameCount,
            }).ToList(),
            ComboStats = state.StatsByComboSig.Select(kv => new ComboStatsDto
            {
                ComboSig = kv.Key,
                Stage = (int)kv.Value.Stage,
                Recent = kv.Value.Recent.Select(r => new MatchRecordDto
                {
                    SelfSegmentAtTime = r.SelfSegmentAtTime,
                    OpponentSegmentAtTime = r.OpponentSegmentAtTime,
                    Outcome = r.Outcome,
                }).ToList(),
            }).ToList(),
        };
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(dto, Options));
    }

    /// <summary>从文件加载状态；若提供 config 则使用其分段等，否则从文件恢复分段配置。</summary>
    public static OptimizerState? Load(string path, ItemDb db, Config? config = null)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<StateDto>(json);
            if (dto == null || dto.Combos == null) return null;
            // 允许加载旧版本；缺失字段将使用默认/空。
            if (dto.Version <= 0 || dto.Version > Version) return null;

            var boundsList = dto.SegmentBounds != null && dto.SegmentBounds.Count > 0
                ? new List<double>(dto.SegmentBounds)
                : new List<double> { 1400, 1600, 1800 };
            Config cfg;
            if (config != null)
            {
                config.SegmentBounds = boundsList;
                cfg = config;
            }
            else
            {
                cfg = new Config
                {
                    SegmentCap = dto.SegmentCap,
                    SegmentBounds = boundsList,
                };
            }
            var state = new OptimizerState(cfg);
            state.TotalRestarts = dto.TotalRestarts;
            state.TotalClimbs = dto.TotalClimbs;
            state.TotalGames = dto.TotalGames;
            state.RngSeed = dto.RngSeed;

            if (dto.Priors != null)
            {
                state.Priors.EmaAlpha = dto.Priors.EmaAlpha > 0 ? dto.Priors.EmaAlpha : state.Priors.EmaAlpha;
                if (dto.Priors.ShapeCountWeights != null)
                {
                    foreach (var kv in dto.Priors.ShapeCountWeights)
                        state.Priors.ShapeCountWeights[kv.Key] = kv.Value;
                }
                if (dto.Priors.ItemWeights != null)
                {
                    foreach (var kv in dto.Priors.ItemWeights)
                        state.Priors.ItemWeights[kv.Key] = kv.Value;
                }
                if (dto.Priors.PairWeights != null)
                {
                    foreach (var kv in dto.Priors.PairWeights)
                        state.Priors.PairWeights[kv.Key] = kv.Value;
                }
                if (dto.Priors.MechanicPairWeights != null)
                {
                    foreach (var kv in dto.Priors.MechanicPairWeights)
                        state.Priors.MechanicPairWeights[kv.Key] = kv.Value;
                }
            }

            foreach (var c in dto.Combos)
            {
                if (c.ComboSig == null || c.RepresentativeShape == null || c.RepresentativeItems == null)
                    continue;
                if (c.RepresentativeShape.Count != c.RepresentativeItems.Count)
                    continue;
                var rep = new DeckRep(c.RepresentativeShape, c.RepresentativeItems);
                state.Pool[c.ComboSig] = new ComboEntry(c.ComboSig, rep, c.Elo, c.IsLocalOptimum, c.IsConfirmed, c.GameCount);
            }

            // fast lane 统计（v3 起）
            if (dto.ComboStats != null)
            {
                foreach (var s in dto.ComboStats)
                {
                    if (string.IsNullOrEmpty(s.ComboSig)) continue;
                    var stats = new OptimizerState.ComboStats();
                    stats.Stage = Enum.IsDefined(typeof(OptimizerState.FastLaneStage), s.Stage)
                        ? (OptimizerState.FastLaneStage)s.Stage
                        : OptimizerState.FastLaneStage.None;
                    if (s.Recent != null)
                    {
                        foreach (var r in s.Recent)
                        {
                            stats.Recent.Add(new OptimizerState.MatchRecord(
                                r.SelfSegmentAtTime,
                                r.OpponentSegmentAtTime,
                                r.Outcome));
                        }
                    }
                    state.StatsByComboSig[s.ComboSig] = stats;
                }
            }
            return state;
        }
        catch
        {
            return null;
        }
    }

    private class StateDto
    {
        public int Version { get; set; }
        public List<double>? SegmentBounds { get; set; }
        public int SegmentCap { get; set; }
        public int TotalRestarts { get; set; }
        public int TotalClimbs { get; set; }
        public int TotalGames { get; set; }
        public int? RngSeed { get; set; }
        public SummaryDto? Summary { get; set; }
        public ConfigSnapshotDto? ConfigSnapshot { get; set; }
        public PriorsDto? Priors { get; set; }
        public List<ComboDto>? Combos { get; set; }
        public List<ComboStatsDto>? ComboStats { get; set; }
    }

    private class SummaryDto
    {
        public int PoolSize { get; set; }
        public int ConfirmedCount { get; set; }
        public int LocalOptimaCount { get; set; }
        public double MaxElo { get; set; }
        public double MinElo { get; set; }
    }

    private class ConfigSnapshotDto
    {
        public int TopInterval { get; set; }
        public int SaveInterval { get; set; }
        public int SegmentCap { get; set; }
        public double InitialElo { get; set; }
        public double EloK { get; set; }
        public int GamesPerEval { get; set; }
        public int MaxClimbSteps { get; set; }
        public int NeighborSampleSize { get; set; }
        public int MabBudgetPerStep { get; set; }
        public int InnerWars { get; set; }
        public int InnerBudget { get; set; }
        public int InnerSelectTop { get; set; }
        public int InnerSelectWars { get; set; }
        public int ConfirmOpponents { get; set; }
        public int ConfirmGamesPerOpponent { get; set; }
        public double ExploreMix { get; set; }
        public double PriorEmaAlpha { get; set; }
        public double SynergyPairLambda { get; set; }
        public double SynergyMechanicLambda { get; set; }
        public double AnchoredMix { get; set; }
        public int AnchoredReportCount { get; set; }
        public int InjectInterval { get; set; }
        public int InjectCount { get; set; }
        public int Workers { get; set; }
        public bool FastLaneEnabled { get; set; }
        public double FastLaneEloDeltaThreshold { get; set; }
        public double PriorsSignalClip { get; set; }
        public double PriorsUnconfirmedMultiplier { get; set; }
        public int PriorsMinGamesForFullWeight { get; set; }
        public int PriorsAnnealGames { get; set; }
        public double CandidateRandomMixMin { get; set; }
        public double CandidateItemOnlyMixStart { get; set; }
        public double CandidateItemOnlyMixEnd { get; set; }
        public int RerateIntervalClimbs { get; set; }
        public int RerateBatchSize { get; set; }
        public int RerateGamesPerDeck { get; set; }
    }

    private class PriorsDto
    {
        public double EmaAlpha { get; set; }
        public Dictionary<string, double>? ShapeCountWeights { get; set; }
        public Dictionary<string, double>? ItemWeights { get; set; }
        public Dictionary<string, double>? PairWeights { get; set; }
        public Dictionary<string, double>? MechanicPairWeights { get; set; }
    }

    private class ComboDto
    {
        public string? ComboSig { get; set; }
        public List<int>? RepresentativeShape { get; set; }
        public List<string>? RepresentativeItems { get; set; }
        public double Elo { get; set; }
        public bool IsLocalOptimum { get; set; }
        public bool IsConfirmed { get; set; }
        public int GameCount { get; set; }
    }

    private class ComboStatsDto
    {
        public string? ComboSig { get; set; }
        public int Stage { get; set; }
        public List<MatchRecordDto>? Recent { get; set; }
    }

    private class MatchRecordDto
    {
        public int SelfSegmentAtTime { get; set; }
        public int OpponentSegmentAtTime { get; set; }
        public sbyte Outcome { get; set; }
    }
}
