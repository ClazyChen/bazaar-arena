namespace BazaarArena.GreedyDeckFinder;

public sealed class PerfStats
{
    private readonly long _wallStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
    private long _expandTicks;
    private long _repTicks;
    private long _swissTicks;
    private long _roundRobinTicks;
    private long _singleGameTicks;
    private long _boTicks;
    private long _singleGames;
    private long _boSeries;
    private long _repCandidates;
    private long _repBoSeries;

    public void AddExpandTicks(long ticks) => System.Threading.Interlocked.Add(ref _expandTicks, ticks);
    public void AddRepTicks(long ticks) => System.Threading.Interlocked.Add(ref _repTicks, ticks);
    public void AddSwissTicks(long ticks) => System.Threading.Interlocked.Add(ref _swissTicks, ticks);
    public void AddRoundRobinTicks(long ticks) => System.Threading.Interlocked.Add(ref _roundRobinTicks, ticks);
    public void AddSingleGameTicks(long ticks) => System.Threading.Interlocked.Add(ref _singleGameTicks, ticks);
    public void AddBoTicks(long ticks) => System.Threading.Interlocked.Add(ref _boTicks, ticks);
    public void IncSingleGame() => System.Threading.Interlocked.Increment(ref _singleGames);
    public void IncBoSeries() => System.Threading.Interlocked.Increment(ref _boSeries);
    public void AddRepCandidates(int count) => System.Threading.Interlocked.Add(ref _repCandidates, count);
    public void AddRepBoSeries(int count) => System.Threading.Interlocked.Add(ref _repBoSeries, count);

    private static double ToMs(long ticks) => ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

    public string BuildSummary()
    {
        var expandMs = ToMs(System.Threading.Interlocked.Read(ref _expandTicks));
        var repMs = ToMs(System.Threading.Interlocked.Read(ref _repTicks));
        var swissMs = ToMs(System.Threading.Interlocked.Read(ref _swissTicks));
        var rrMs = ToMs(System.Threading.Interlocked.Read(ref _roundRobinTicks));
        var singleMs = ToMs(System.Threading.Interlocked.Read(ref _singleGameTicks));
        var boMs = ToMs(System.Threading.Interlocked.Read(ref _boTicks));
        var games = System.Threading.Interlocked.Read(ref _singleGames);
        var bos = System.Threading.Interlocked.Read(ref _boSeries);
        var repCandidates = System.Threading.Interlocked.Read(ref _repCandidates);
        var repBos = System.Threading.Interlocked.Read(ref _repBoSeries);
        var throughput = singleMs <= 0.0001 ? 0 : games / (singleMs / 1000.0);
        var wallMs = ToMs(System.Diagnostics.Stopwatch.GetTimestamp() - _wallStartTicks);
        var wallThroughput = wallMs <= 0.0001 ? 0 : games / (wallMs / 1000.0);
        return
            $"[性能] 扩展={expandMs:F0}ms, 代表排列={repMs:F0}ms, 瑞士轮={swissMs:F0}ms, 大循环={rrMs:F0}ms\n" +
            $"[性能] BO系列={bos}, 单局={games}, BO耗时={boMs:F0}ms, 单局模拟耗时={singleMs:F0}ms, 吞吐(线程累计)={throughput:F1} 局/秒\n" +
            $"[性能] 墙钟耗时={wallMs:F0}ms, 吞吐(墙钟)={wallThroughput:F1} 局/秒\n" +
            $"[性能] 代表排列候选={repCandidates}, 代表排列BO={repBos}";
    }
}

