using System.Threading;

namespace BazaarArena.Core;

/// <summary>线程局部随机数：避免 Random.Shared 在高并发下的争用。</summary>
public static class ThreadLocalRandom
{
    private static int _seed = Environment.TickCount;

    private static readonly ThreadLocal<Random> _rng = new(() =>
    {
        int s = Interlocked.Increment(ref _seed);
        // 混入线程 id，降低相邻种子相关性
        unchecked { s = (s * 1664525) + 1013904223 + Environment.CurrentManagedThreadId; }
        return new Random(s);
    });

    public static int Next(int maxExclusive) => _rng.Value!.Next(maxExclusive);
    public static int Next(int minInclusive, int maxExclusive) => _rng.Value!.Next(minInclusive, maxExclusive);
    public static int Next100() => _rng.Value!.Next(100);
}

