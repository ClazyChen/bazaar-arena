using System.Text.Json;
using BazaarArena.ItemDatabase;
using ItemDb = BazaarArena.ItemDatabase.ItemDatabase;

namespace BazaarArena.QualityDeckFinder;

public static class StatePersistence
{
    private const int Version = 1;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static void Save(string path, OptimizerState state)
    {
        var dto = new StateDto
        {
            Version = Version,
            SegmentBounds = state.Config.SegmentBounds.ToList(),
            SegmentCap = state.Config.SegmentCap,
            TotalRestarts = state.TotalRestarts,
            TotalClimbs = state.TotalClimbs,
            TotalGames = state.TotalGames,
            RngSeed = state.RngSeed,
            Decks = state.Pool.Select(kv => new DeckDto
            {
                Shape = kv.Value.Deck.Shape.ToList(),
                ItemNames = kv.Value.Deck.ItemNames.ToList(),
                Elo = kv.Value.Elo,
                IsLocalOptimum = kv.Value.IsLocalOptimum,
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
            if (dto == null || dto.Decks == null) return null;

            var cfg = config ?? new Config
            {
                SegmentCap = dto.SegmentCap,
                SegmentBounds = dto.SegmentBounds ?? [1400, 1600, 1800],
            };
            var state = new OptimizerState(cfg);
            state.TotalRestarts = dto.TotalRestarts;
            state.TotalClimbs = dto.TotalClimbs;
            state.TotalGames = dto.TotalGames;
            state.RngSeed = dto.RngSeed;

            foreach (var d in dto.Decks)
            {
                if (d.Shape == null || d.ItemNames == null || d.Shape.Count != d.ItemNames.Count)
                    continue;
                var rep = new DeckRep(d.Shape, d.ItemNames);
                var sig = rep.Signature();
                state.Pool[sig] = new DeckEntry(rep, d.Elo, d.IsLocalOptimum, d.GameCount);
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
        public List<DeckDto>? Decks { get; set; }
    }

    private class DeckDto
    {
        public List<int>? Shape { get; set; }
        public List<string>? ItemNames { get; set; }
        public double Elo { get; set; }
        public bool IsLocalOptimum { get; set; }
        public int GameCount { get; set; }
    }
}
