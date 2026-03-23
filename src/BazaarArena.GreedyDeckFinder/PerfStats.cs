using System.Text;

namespace BazaarArena.GreedyDeckFinder;

/// <summary>贪心搜索性能统计；<see cref="BuildSummary"/> 在 <c>--perf</c> 时输出。</summary>
public sealed class PerfStats
{
    /// <summary>直方图下标 1..32 为精确对局数，33 表示 ≥33 对。</summary>
    private const int BatchHistUpperExclusive = 34;

    private readonly long _wallStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
    private long _expandTicks;
    private long _repTicks;
    private long _swissTicks;
    private long _roundRobinTicks;
    private long _playoffTicks;
    private long _singleGameTicks;
    private long _boTicks;
    private long _singleGames;
    private long _boSeries;
    private long _repCandidates;
    private long _repBoSeries;
    private long _swissPruned;

    /// <summary>扩展阶段：组桶、建 repJobs、插入枚举、入桶；不含擂台与对战。</summary>
    private long _expandBucketGlueTicks;
    /// <summary>擂台：初始化锦标赛与洗牌。</summary>
    private long _knockoutInitGlueTicks;
    /// <summary>擂台：每波组对、合并下一轮存活；不含 PlayBoNBatch。</summary>
    private long _knockoutWaveGlueTicks;
    /// <summary>瑞士：重置、剪枝、分桶、建 roundMatches、写分、末位排序；不含 PlayBoNBatch。</summary>
    private long _swissGlueTicks;
    /// <summary>大循环：建对局索引、写分、排序截取；不含 PlaySeriesBatch。</summary>
    private long _roundRobinGlueTicks;
    /// <summary>最终并列加赛：建对、写分、排序；不含 PlaySeriesBatch。</summary>
    private long _playoffGlueTicks;

    private long _boParallelBatchWallTicks;
    private long _boSerialBatchWallTicks;
    private long _seriesParallelBatchWallTicks;
    private long _seriesSerialBatchWallTicks;
    private long _boParallelBatchCount;
    private long _boSerialBatchCount;
    private long _boParallelPairs;
    private long _boSerialPairs;
    private long _seriesParallelBatchCount;
    private long _seriesSerialBatchCount;
    private long _seriesParallelPairs;
    private long _seriesSerialPairs;

    private readonly object _batchSizeLock = new();
    private int _boParPairMin = int.MaxValue;
    private int _boParPairMax;
    private int _boSerPairMin = int.MaxValue;
    private int _boSerPairMax;
    private int _serParPairMin = int.MaxValue;
    private int _serParPairMax;
    private int _serSerPairMin = int.MaxValue;
    private int _serSerPairMax;
    private readonly long[] _boParHist = new long[BatchHistUpperExclusive];
    private readonly long[] _boSerHist = new long[BatchHistUpperExclusive];
    private readonly long[] _serParHist = new long[BatchHistUpperExclusive];
    private readonly long[] _serSerHist = new long[BatchHistUpperExclusive];
    private long _boParBatchesLtWorkers;
    private long _serParBatchesLtWorkers;

    public void AddExpandTicks(long ticks) => System.Threading.Interlocked.Add(ref _expandTicks, ticks);
    public void AddRepTicks(long ticks) => System.Threading.Interlocked.Add(ref _repTicks, ticks);
    public void AddSwissTicks(long ticks) => System.Threading.Interlocked.Add(ref _swissTicks, ticks);
    public void AddRoundRobinTicks(long ticks) => System.Threading.Interlocked.Add(ref _roundRobinTicks, ticks);
    public void AddPlayoffTicks(long ticks) => System.Threading.Interlocked.Add(ref _playoffTicks, ticks);
    public void AddSingleGameTicks(long ticks) => System.Threading.Interlocked.Add(ref _singleGameTicks, ticks);
    public void AddBoTicks(long ticks) => System.Threading.Interlocked.Add(ref _boTicks, ticks);
    public void IncSingleGame() => System.Threading.Interlocked.Increment(ref _singleGames);
    public void IncBoSeries() => System.Threading.Interlocked.Increment(ref _boSeries);
    public void AddRepCandidates(int count) => System.Threading.Interlocked.Add(ref _repCandidates, count);
    public void AddRepBoSeries(int count) => System.Threading.Interlocked.Add(ref _repBoSeries, count);
    public void AddSwissPruned(int count) => System.Threading.Interlocked.Add(ref _swissPruned, count);

    public void AddExpandBucketGlueTicks(long ticks) => System.Threading.Interlocked.Add(ref _expandBucketGlueTicks, ticks);
    public void AddKnockoutInitGlueTicks(long ticks) => System.Threading.Interlocked.Add(ref _knockoutInitGlueTicks, ticks);
    public void AddKnockoutWaveGlueTicks(long ticks) => System.Threading.Interlocked.Add(ref _knockoutWaveGlueTicks, ticks);
    public void AddSwissGlueTicks(long ticks) => System.Threading.Interlocked.Add(ref _swissGlueTicks, ticks);
    public void AddRoundRobinGlueTicks(long ticks) => System.Threading.Interlocked.Add(ref _roundRobinGlueTicks, ticks);
    public void AddPlayoffGlueTicks(long ticks) => System.Threading.Interlocked.Add(ref _playoffGlueTicks, ticks);

    public void RecordBoParallelBatchWall(long ticks, int pairCount, int workersCap)
    {
        System.Threading.Interlocked.Add(ref _boParallelBatchWallTicks, ticks);
        System.Threading.Interlocked.Increment(ref _boParallelBatchCount);
        System.Threading.Interlocked.Add(ref _boParallelPairs, pairCount);
        RecordBoParallelBatchSize(pairCount, workersCap);
    }

    public void RecordBoSerialBatchWall(long ticks, int pairCount)
    {
        System.Threading.Interlocked.Add(ref _boSerialBatchWallTicks, ticks);
        System.Threading.Interlocked.Increment(ref _boSerialBatchCount);
        System.Threading.Interlocked.Add(ref _boSerialPairs, pairCount);
        RecordBoSerialBatchSize(pairCount);
    }

    public void RecordSeriesParallelBatchWall(long ticks, int pairCount, int workersCap)
    {
        System.Threading.Interlocked.Add(ref _seriesParallelBatchWallTicks, ticks);
        System.Threading.Interlocked.Increment(ref _seriesParallelBatchCount);
        System.Threading.Interlocked.Add(ref _seriesParallelPairs, pairCount);
        RecordSeriesParallelBatchSize(pairCount, workersCap);
    }

    public void RecordSeriesSerialBatchWall(long ticks, int pairCount)
    {
        System.Threading.Interlocked.Add(ref _seriesSerialBatchWallTicks, ticks);
        System.Threading.Interlocked.Increment(ref _seriesSerialBatchCount);
        System.Threading.Interlocked.Add(ref _seriesSerialPairs, pairCount);
        RecordSeriesSerialBatchSize(pairCount);
    }

    private void RecordBoParallelBatchSize(int pairCount, int workersCap)
    {
        lock (_batchSizeLock)
        {
            if (pairCount < _boParPairMin) _boParPairMin = pairCount;
            if (pairCount > _boParPairMax) _boParPairMax = pairCount;
            _boParHist[BatchHistIndex(pairCount)]++;
            if (workersCap > 1 && pairCount < workersCap)
                _boParBatchesLtWorkers++;
        }
    }

    private void RecordBoSerialBatchSize(int pairCount)
    {
        lock (_batchSizeLock)
        {
            if (pairCount < _boSerPairMin) _boSerPairMin = pairCount;
            if (pairCount > _boSerPairMax) _boSerPairMax = pairCount;
            _boSerHist[BatchHistIndex(pairCount)]++;
        }
    }

    private void RecordSeriesParallelBatchSize(int pairCount, int workersCap)
    {
        lock (_batchSizeLock)
        {
            if (pairCount < _serParPairMin) _serParPairMin = pairCount;
            if (pairCount > _serParPairMax) _serParPairMax = pairCount;
            _serParHist[BatchHistIndex(pairCount)]++;
            if (workersCap > 1 && pairCount < workersCap)
                _serParBatchesLtWorkers++;
        }
    }

    private void RecordSeriesSerialBatchSize(int pairCount)
    {
        lock (_batchSizeLock)
        {
            if (pairCount < _serSerPairMin) _serSerPairMin = pairCount;
            if (pairCount > _serSerPairMax) _serSerPairMax = pairCount;
            _serSerHist[BatchHistIndex(pairCount)]++;
        }
    }

    private static int BatchHistIndex(int pairCount) =>
        pairCount >= 33 ? 33 : Math.Max(0, pairCount);

    private static string FormatBatchHist(long[] hist, int minBucket)
    {
        var sb = new StringBuilder();
        for (int i = minBucket; i < hist.Length; i++)
        {
            if (hist[i] == 0) continue;
            if (sb.Length > 0) sb.Append(' ');
            string label = i == 33 ? "≥33" : i.ToString();
            sb.Append(label).Append('×').Append(hist[i]);
        }
        return sb.Length == 0 ? "—" : sb.ToString();
    }

    private static double ToMs(long ticks) => ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

    /// <param name="parallelWorkers"><see cref="BattleEvaluator"/> 的 <c>MaxDegreeOfParallelism</c>，用于「对局数 &lt; workers」占比。</param>
    public string BuildSummary(int parallelWorkers = 1)
    {
        var expandMs = ToMs(System.Threading.Interlocked.Read(ref _expandTicks));
        var repMs = ToMs(System.Threading.Interlocked.Read(ref _repTicks));
        var swissMs = ToMs(System.Threading.Interlocked.Read(ref _swissTicks));
        var rrMs = ToMs(System.Threading.Interlocked.Read(ref _roundRobinTicks));
        var playoffMs = ToMs(System.Threading.Interlocked.Read(ref _playoffTicks));
        var singleMs = ToMs(System.Threading.Interlocked.Read(ref _singleGameTicks));
        var boMs = ToMs(System.Threading.Interlocked.Read(ref _boTicks));
        var games = System.Threading.Interlocked.Read(ref _singleGames);
        var bos = System.Threading.Interlocked.Read(ref _boSeries);
        var repCandidates = System.Threading.Interlocked.Read(ref _repCandidates);
        var repBos = System.Threading.Interlocked.Read(ref _repBoSeries);
        var swissPruned = System.Threading.Interlocked.Read(ref _swissPruned);
        var throughput = singleMs <= 0.0001 ? 0 : games / (singleMs / 1000.0);
        var wallMs = ToMs(System.Diagnostics.Stopwatch.GetTimestamp() - _wallStartTicks);
        var wallThroughput = wallMs <= 0.0001 ? 0 : games / (wallMs / 1000.0);

        var bucketGlueMs = ToMs(System.Threading.Interlocked.Read(ref _expandBucketGlueTicks));
        var koInitMs = ToMs(System.Threading.Interlocked.Read(ref _knockoutInitGlueTicks));
        var koWaveMs = ToMs(System.Threading.Interlocked.Read(ref _knockoutWaveGlueTicks));
        var swissGlueMs = ToMs(System.Threading.Interlocked.Read(ref _swissGlueTicks));
        var rrGlueMs = ToMs(System.Threading.Interlocked.Read(ref _roundRobinGlueTicks));
        var playoffGlueMs = ToMs(System.Threading.Interlocked.Read(ref _playoffGlueTicks));
        var glueSumMs = bucketGlueMs + koInitMs + koWaveMs + swissGlueMs + rrGlueMs + playoffGlueMs;

        var boParWallMs = ToMs(System.Threading.Interlocked.Read(ref _boParallelBatchWallTicks));
        var boSerWallMs = ToMs(System.Threading.Interlocked.Read(ref _boSerialBatchWallTicks));
        var serParWallMs = ToMs(System.Threading.Interlocked.Read(ref _seriesParallelBatchWallTicks));
        var serSerWallMs = ToMs(System.Threading.Interlocked.Read(ref _seriesSerialBatchWallTicks));
        var batchWallSumMs = boParWallMs + boSerWallMs + serParWallMs + serSerWallMs;

        var boParBatches = System.Threading.Interlocked.Read(ref _boParallelBatchCount);
        var boSerBatches = System.Threading.Interlocked.Read(ref _boSerialBatchCount);
        var boParPairs = System.Threading.Interlocked.Read(ref _boParallelPairs);
        var boSerPairs = System.Threading.Interlocked.Read(ref _boSerialPairs);
        var serParBatches = System.Threading.Interlocked.Read(ref _seriesParallelBatchCount);
        var serSerBatches = System.Threading.Interlocked.Read(ref _seriesSerialBatchCount);
        var serParPairs = System.Threading.Interlocked.Read(ref _seriesParallelPairs);
        var serSerPairs = System.Threading.Interlocked.Read(ref _seriesSerialPairs);
        var boPairsTotal = boParPairs + boSerPairs;
        var serPairsTotal = serParPairs + serSerPairs;

        var phaseSumMs = expandMs + swissMs + rrMs + playoffMs;
        var reconcileMs = glueSumMs + batchWallSumMs;
        var reconcileDiffPct = phaseSumMs <= 0.1 ? 0 : Math.Abs(reconcileMs - phaseSumMs) / phaseSumMs * 100.0;

        var glueRatio = phaseSumMs <= 0.1 ? 0 : glueSumMs / phaseSumMs * 100.0;
        var parallelBoRatio = phaseSumMs <= 0.1 ? 0 : boParWallMs / phaseSumMs * 100.0;
        var serialBoBatchRatio = boPairsTotal <= 0 ? 0 : boSerPairs * 100.0 / boPairsTotal;
        var parEff = wallMs <= 0.1 ? 0 : singleMs / wallMs;

        var wCap = parallelWorkers < 1 ? 1 : parallelWorkers;
        long boParLt = System.Threading.Interlocked.Read(ref _boParBatchesLtWorkers);
        long serParLt = System.Threading.Interlocked.Read(ref _serParBatchesLtWorkers);
        var boParLtPct = boParBatches <= 0 ? 0 : boParLt * 100.0 / boParBatches;
        var serParLtPct = serParBatches <= 0 ? 0 : serParLt * 100.0 / serParBatches;
        var boParMean = boParBatches <= 0 ? 0 : boParPairs / (double)boParBatches;
        var boSerMean = boSerBatches <= 0 ? 0 : boSerPairs / (double)boSerBatches;
        var serParMean = serParBatches <= 0 ? 0 : serParPairs / (double)serParBatches;
        var serSerMean = serSerBatches <= 0 ? 0 : serSerPairs / (double)serSerBatches;

        string boParMinStr;
        string boParMaxStr;
        string boSerMinStr;
        string boSerMaxStr;
        string serParMinStr;
        string serParMaxStr;
        string serSerMinStr;
        string serSerMaxStr;
        string boParHistStr;
        string boSerHistStr;
        string serParHistStr;
        string serSerHistStr;
        lock (_batchSizeLock)
        {
            boParMinStr = _boParPairMin == int.MaxValue ? "—" : _boParPairMin.ToString();
            boParMaxStr = boParBatches <= 0 ? "—" : _boParPairMax.ToString();
            boSerMinStr = _boSerPairMin == int.MaxValue ? "—" : _boSerPairMin.ToString();
            boSerMaxStr = boSerBatches <= 0 ? "—" : _boSerPairMax.ToString();
            serParMinStr = _serParPairMin == int.MaxValue ? "—" : _serParPairMin.ToString();
            serParMaxStr = serParBatches <= 0 ? "—" : _serParPairMax.ToString();
            serSerMinStr = _serSerPairMin == int.MaxValue ? "—" : _serSerPairMin.ToString();
            serSerMaxStr = serSerBatches <= 0 ? "—" : _serSerPairMax.ToString();
            boParHistStr = FormatBatchHist(_boParHist, minBucket: 2);
            boSerHistStr = FormatBatchHist(_boSerHist, minBucket: 1);
            serParHistStr = FormatBatchHist(_serParHist, minBucket: 2);
            serSerHistStr = FormatBatchHist(_serSerHist, minBucket: 1);
        }

        var line1 =
            $"[性能] 扩展={expandMs:F0}ms, 代表排列={repMs:F0}ms, 瑞士轮={swissMs:F0}ms, 大循环={rrMs:F0}ms, 加赛={playoffMs:F0}ms\n" +
            $"[性能] BO系列={bos}, 单局={games}, BO耗时={boMs:F0}ms, 单局模拟耗时={singleMs:F0}ms, 吞吐(线程累计)={throughput:F1} 局/秒\n" +
            $"[性能] 墙钟耗时={wallMs:F0}ms, 吞吐(墙钟)={wallThroughput:F1} 局/秒\n" +
            $"[性能] 代表排列候选={repCandidates}, 代表排列BO={repBos}, 瑞士剪枝淘汰={swissPruned}";

        var line2 =
            $"[性能·分解] 阶段墙钟合计(扩+瑞+循+加)={phaseSumMs:F0}ms | 胶水合计={glueSumMs:F0}ms({glueRatio:F1}%) 其中 扩展桶={bucketGlueMs:F0} 擂台初始化={koInitMs:F0} 擂台波次={koWaveMs:F0} 瑞士={swissGlueMs:F0} 大循环={rrGlueMs:F0} 加赛={playoffGlueMs:F0}\n" +
            $"[性能·分解] 对战批墙钟 BO并行={boParWallMs:F0}ms({boParBatches}批,{boParPairs}对) BO串行={boSerWallMs:F0}ms({boSerBatches}批,{boSerPairs}对) BO串行对占比={serialBoBatchRatio:F1}%\n" +
            $"[性能·分解] 系列赛批墙钟 并行={serParWallMs:F0}ms({serParBatches}批,{serParPairs}对) 串行={serSerWallMs:F0}ms({serSerBatches}批,{serSerPairs}对)\n" +
            $"[性能·分解] 胶水+各批墙钟={reconcileMs:F0}ms 与阶段合计偏差={reconcileDiffPct:F1}% (应接近0；偏差大则存在未计入区间)\n" +
            $"[性能·分解] 并行BO墙钟占阶段={parallelBoRatio:F1}% | 单局线程累计/perf墙钟≈{parEff:F2}(理想多并行时>1)";

        var line3 =
            $"[性能·批大小] workers={wCap} | BO并行批 对数/批均值={boParMean:F2} 最小={boParMinStr} 最大={boParMaxStr} 对数<workers批占比={boParLtPct:F1}% (批内并行度上限=min(对数,workers))\n" +
            $"[性能·批大小] BO并行批对数分布(仅非零桶) {boParHistStr}\n" +
            $"[性能·批大小] BO串行批 均值={boSerMean:F2} 最小={boSerMinStr} 最大={boSerMaxStr} | 分布 {boSerHistStr}\n" +
            $"[性能·批大小] 系列赛并行批 均值={serParMean:F2} 最小={serParMinStr} 最大={serParMaxStr} 对数<workers批占比={serParLtPct:F1}% | 分布 {serParHistStr}\n" +
            $"[性能·批大小] 系列赛串行批 均值={serSerMean:F2} 最小={serSerMinStr} 最大={serSerMaxStr} | 分布 {serSerHistStr}";

        return line1 + "\n" + line2 + "\n" + line3;
    }
}
