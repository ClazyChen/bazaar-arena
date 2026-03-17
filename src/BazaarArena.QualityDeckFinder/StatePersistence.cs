using System.Text.Encodings.Web;
using System.Text.Json;
using BazaarArena.ItemDatabase;
using ItemDb = BazaarArena.ItemDatabase.ItemDatabase;

namespace BazaarArena.QualityDeckFinder;

public static class StatePersistence
{
    private const int Version = 2;
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
        var dto = new StateDto
        {
            Version = Version,
            SegmentBounds = boundsSnapshot,
            SegmentCap = state.Config.SegmentCap,
            TotalRestarts = state.TotalRestarts,
            TotalClimbs = state.TotalClimbs,
            TotalGames = state.TotalGames,
            RngSeed = state.RngSeed,
            Priors = new PriorsDto
            {
                EmaAlpha = state.Priors.EmaAlpha,
                ShapeCountWeights = state.Priors.ShapeCountWeights.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
                ItemWeights = state.Priors.ItemWeights.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
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
            if (dto == null || dto.Version != Version || dto.Combos == null) return null;

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
        public PriorsDto? Priors { get; set; }
        public List<ComboDto>? Combos { get; set; }
    }

    private class PriorsDto
    {
        public double EmaAlpha { get; set; }
        public Dictionary<string, double>? ShapeCountWeights { get; set; }
        public Dictionary<string, double>? ItemWeights { get; set; }
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
}
