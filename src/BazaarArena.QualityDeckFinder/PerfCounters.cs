using System.Diagnostics;

namespace BazaarArena.QualityDeckFinder;

/// <summary>低开销性能计数器：用于阶段耗时与对局吞吐统计（不影响算法结果）。</summary>
public static class PerfCounters
{
    public static bool Enabled { get; set; }

    // --- run scoped ---
    private static long _runStartTimestamp;

    // --- season scoped ---
    private static long _seasonStartTimestamp;
    private static int _seasonStartTotalGames;
    private static long _seasonId;

    private static long _repTicks;
    private static long _matchScheduleTicks;
    private static long _matchRunTicks;
    private static long _matchApplyTicks;
    private static long _matchSelectOpponentsTicks;
    private static long _hillClimbStrengthTicks;
    private static long _hillClimbAnchoredTicks;
    private static long _hillEvalBuildTicks;
    private static long _hillEvalSimulateTicks;
    private static long _hillEvalApplyTicks;
    private static long _hillNeighborSampleTicks;
    private static long _hillNeighborShuffleTicks;
    private static long _hillEnsureRepresentativeTicks;
    private static long _hillSelectOpponentsTicks;
    private static long _hillMabTicks;
    private static long _abandonInjectTicks;

    private static long _matchGames;
    private static long _matchRounds;

    // --- battle runs (cross-cutting) ---
    private static long _battleRunTicks;
    private static long _battleRunCount;
    private static long _deckBuildTicks;
    private static long _deckBuildCount;

    private sealed class LocalCounters
    {
        public long SeasonId;
        public long BattleRunTicks;
        public long BattleRunCount;
        public long DeckBuildTicks;
        public long DeckBuildCount;
    }

    private static readonly ThreadLocal<LocalCounters> _local = new(() => new LocalCounters(), trackAllValues: true);

    public static void SeasonBegin(int totalGames)
    {
        if (!Enabled) return;
        if (Interlocked.Read(ref _runStartTimestamp) == 0)
            Interlocked.CompareExchange(ref _runStartTimestamp, Stopwatch.GetTimestamp(), 0);
        _seasonStartTimestamp = Stopwatch.GetTimestamp();
        _seasonStartTotalGames = totalGames;
        Interlocked.Increment(ref _seasonId);

        _repTicks = 0;
        _matchScheduleTicks = 0;
        _matchRunTicks = 0;
        _matchApplyTicks = 0;
        _matchSelectOpponentsTicks = 0;
        _hillClimbStrengthTicks = 0;
        _hillClimbAnchoredTicks = 0;
        _hillEvalBuildTicks = 0;
        _hillEvalSimulateTicks = 0;
        _hillEvalApplyTicks = 0;
        _hillNeighborSampleTicks = 0;
        _hillNeighborShuffleTicks = 0;
        _hillEnsureRepresentativeTicks = 0;
        _hillSelectOpponentsTicks = 0;
        _hillMabTicks = 0;
        _abandonInjectTicks = 0;
        _matchGames = 0;
        _matchRounds = 0;

        // 全局字段保留但不再作为热点写入（Record* 改为 thread-local）
        _battleRunTicks = 0;
        _battleRunCount = 0;
        _deckBuildTicks = 0;
        _deckBuildCount = 0;
    }

    public static void AddRepTicks(long ticks) { if (Enabled) Interlocked.Add(ref _repTicks, ticks); }
    public static void AddMatchScheduleTicks(long ticks) { if (Enabled) Interlocked.Add(ref _matchScheduleTicks, ticks); }
    public static void AddMatchRunTicks(long ticks) { if (Enabled) Interlocked.Add(ref _matchRunTicks, ticks); }
    public static void AddMatchApplyTicks(long ticks) { if (Enabled) Interlocked.Add(ref _matchApplyTicks, ticks); }
    public static void AddMatchSelectOpponentsTicks(long ticks) { if (Enabled) Interlocked.Add(ref _matchSelectOpponentsTicks, ticks); }
    public static void AddHillClimbStrengthTicks(long ticks) { if (Enabled) Interlocked.Add(ref _hillClimbStrengthTicks, ticks); }
    public static void AddHillClimbAnchoredTicks(long ticks) { if (Enabled) Interlocked.Add(ref _hillClimbAnchoredTicks, ticks); }
    public static void AddHillEvalBuildTicks(long ticks) { if (Enabled) Interlocked.Add(ref _hillEvalBuildTicks, ticks); }
    public static void AddHillEvalSimulateTicks(long ticks) { if (Enabled) Interlocked.Add(ref _hillEvalSimulateTicks, ticks); }
    public static void AddHillEvalApplyTicks(long ticks) { if (Enabled) Interlocked.Add(ref _hillEvalApplyTicks, ticks); }
    public static void AddHillNeighborSampleTicks(long ticks) { if (Enabled) Interlocked.Add(ref _hillNeighborSampleTicks, ticks); }
    public static void AddHillNeighborShuffleTicks(long ticks) { if (Enabled) Interlocked.Add(ref _hillNeighborShuffleTicks, ticks); }
    public static void AddHillEnsureRepresentativeTicks(long ticks) { if (Enabled) Interlocked.Add(ref _hillEnsureRepresentativeTicks, ticks); }
    public static void AddHillSelectOpponentsTicks(long ticks) { if (Enabled) Interlocked.Add(ref _hillSelectOpponentsTicks, ticks); }
    public static void AddHillMabTicks(long ticks) { if (Enabled) Interlocked.Add(ref _hillMabTicks, ticks); }
    public static void AddAbandonInjectTicks(long ticks) { if (Enabled) Interlocked.Add(ref _abandonInjectTicks, ticks); }

    public static void AddMatchGames(int games) { if (Enabled) Interlocked.Add(ref _matchGames, games); }
    public static void AddMatchRound() { if (Enabled) Interlocked.Increment(ref _matchRounds); }

    public static void RecordBattleRun(long ticks)
    {
        if (!Enabled) return;
        // 热点：并行跑局时每局都会调用。用 thread-local 累加，赛季结束再汇总，避免全局原子争用。
        var lc = _local.Value!;
        long sid = Volatile.Read(ref _seasonId);
        if (lc.SeasonId != sid)
        {
            lc.SeasonId = sid;
            lc.BattleRunTicks = 0;
            lc.BattleRunCount = 0;
            lc.DeckBuildTicks = 0;
            lc.DeckBuildCount = 0;
        }
        lc.BattleRunTicks += ticks;
        lc.BattleRunCount++;
    }

    public static void RecordDeckBuild(long ticks)
    {
        if (!Enabled) return;
        var lc = _local.Value!;
        long sid = Volatile.Read(ref _seasonId);
        if (lc.SeasonId != sid)
        {
            lc.SeasonId = sid;
            lc.BattleRunTicks = 0;
            lc.BattleRunCount = 0;
            lc.DeckBuildTicks = 0;
            lc.DeckBuildCount = 0;
        }
        lc.DeckBuildTicks += ticks;
        lc.DeckBuildCount++;
    }

    private static double TicksToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    public static void PrintSeasonSummary(int seasonNumber1Based, int totalGamesEnd)
    {
        if (!Enabled) return;

        var seasonElapsedMs = TicksToMs(Stopwatch.GetTimestamp() - _seasonStartTimestamp);
        int seasonGamesDelta = Math.Max(0, totalGamesEnd - _seasonStartTotalGames);
        double gamesPerSec = seasonElapsedMs <= 0 ? 0 : seasonGamesDelta / (seasonElapsedMs / 1000.0);

        var runStart = Interlocked.Read(ref _runStartTimestamp);
        var runElapsedMs = runStart == 0 ? 0.0 : TicksToMs(Stopwatch.GetTimestamp() - runStart);
        double avgGamesPerSec = runElapsedMs <= 0 ? 0 : totalGamesEnd / (runElapsedMs / 1000.0);

        double repMs = TicksToMs(Interlocked.Read(ref _repTicks));
        double matchScheduleMs = TicksToMs(Interlocked.Read(ref _matchScheduleTicks));
        double matchRunMs = TicksToMs(Interlocked.Read(ref _matchRunTicks));
        double matchApplyMs = TicksToMs(Interlocked.Read(ref _matchApplyTicks));
        double matchSelectOppMs = TicksToMs(Interlocked.Read(ref _matchSelectOpponentsTicks));
        double hillStrengthMs = TicksToMs(Interlocked.Read(ref _hillClimbStrengthTicks));
        double hillAnchoredMs = TicksToMs(Interlocked.Read(ref _hillClimbAnchoredTicks));
        double hillEvalBuildMs = TicksToMs(Interlocked.Read(ref _hillEvalBuildTicks));
        double hillEvalSimulateMs = TicksToMs(Interlocked.Read(ref _hillEvalSimulateTicks));
        double hillEvalApplyMs = TicksToMs(Interlocked.Read(ref _hillEvalApplyTicks));
        double hillNeighborSampleMs = TicksToMs(Interlocked.Read(ref _hillNeighborSampleTicks));
        double hillNeighborShuffleMs = TicksToMs(Interlocked.Read(ref _hillNeighborShuffleTicks));
        double hillEnsureRepMs = TicksToMs(Interlocked.Read(ref _hillEnsureRepresentativeTicks));
        double hillSelectOppMs = TicksToMs(Interlocked.Read(ref _hillSelectOpponentsTicks));
        double hillMabMs = TicksToMs(Interlocked.Read(ref _hillMabTicks));
        double abandonInjectMs = TicksToMs(Interlocked.Read(ref _abandonInjectTicks));

        // 汇总 thread-local（仅统计当前赛季）
        long sid = Volatile.Read(ref _seasonId);
        long battleCount = 0;
        long battleTicks = 0;
        long deckBuildCount = 0;
        long deckBuildTicks = 0;
        foreach (var lc in _local.Values)
        {
            if (lc == null || lc.SeasonId != sid) continue;
            battleCount += lc.BattleRunCount;
            battleTicks += lc.BattleRunTicks;
            deckBuildCount += lc.DeckBuildCount;
            deckBuildTicks += lc.DeckBuildTicks;
        }
        // 同时保留全局字段，便于未来扩展（当前主要靠 thread-local）
        battleCount += Interlocked.Read(ref _battleRunCount);
        battleTicks += Interlocked.Read(ref _battleRunTicks);
        deckBuildCount += Interlocked.Read(ref _deckBuildCount);
        deckBuildTicks += Interlocked.Read(ref _deckBuildTicks);
        double battleMs = TicksToMs(battleTicks);
        double deckBuildMs = TicksToMs(deckBuildTicks);

        if (runElapsedMs > 0)
            Console.WriteLine($"[性能] 已运行 {runElapsedMs / 1000.0:F1}s，总对局 {totalGamesEnd}，平均吞吐 {avgGamesPerSec:F2} 局/秒");
        Console.WriteLine($"[性能] 赛季 {seasonNumber1Based} 总耗时 {seasonElapsedMs:F0}ms，对局增量 {seasonGamesDelta}，吞吐 {gamesPerSec:F2} 局/秒");
        Console.WriteLine($"[性能]  代表选择 {repMs:F0}ms");
        Console.WriteLine($"[性能]  匹配赛：轮数 {Interlocked.Read(ref _matchRounds)}，赛程构造 {matchScheduleMs:F0}ms，跑局 {matchRunMs:F0}ms，合并写池 {matchApplyMs:F0}ms，本季匹配局数 {Interlocked.Read(ref _matchGames)}");
        if (matchSelectOppMs > 0)
            Console.WriteLine($"[性能]  匹配赛细分：选对手 {matchSelectOppMs:F0}ms");
        Console.WriteLine($"[性能]  卡组优化：强度 {hillStrengthMs:F0}ms，锚定 {hillAnchoredMs:F0}ms");
        if (hillEvalBuildMs > 0 || hillEvalSimulateMs > 0 || hillEvalApplyMs > 0)
            Console.WriteLine($"[性能]  HillClimb 评估细分：构建对局 {hillEvalBuildMs:F0}ms，模拟对局 {hillEvalSimulateMs:F0}ms，顺序写回 {hillEvalApplyMs:F0}ms");
        if (hillNeighborSampleMs > 0 || hillEnsureRepMs > 0 || hillSelectOppMs > 0 || hillMabMs > 0 || hillNeighborShuffleMs > 0)
            Console.WriteLine($"[性能]  HillClimb 其它细分：邻域采样 {hillNeighborSampleMs:F0}ms，洗牌/随机序 {hillNeighborShuffleMs:F0}ms，EnsureRepresentative {hillEnsureRepMs:F0}ms，选对手 {hillSelectOppMs:F0}ms，MAB/循环控制 {hillMabMs:F0}ms");
        Console.WriteLine($"[性能]  放弃/注入 {abandonInjectMs:F0}ms");
        if (battleCount > 0)
            Console.WriteLine($"[性能]  对战模拟 Run()：{battleCount} 次，合计 {battleMs:F0}ms，均值 {(battleMs / battleCount):F3}ms/局");
        if (deckBuildCount > 0)
            Console.WriteLine($"[性能]  构建 Deck(ToDeck/BuildSide)：{deckBuildCount} 次，合计 {deckBuildMs:F0}ms，均值 {(deckBuildMs / deckBuildCount):F3}ms/次");
    }
}

