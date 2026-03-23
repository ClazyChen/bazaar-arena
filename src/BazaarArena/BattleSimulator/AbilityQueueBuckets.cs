using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>
/// 能力队列按 <see cref="AbilityPriority"/> 分 6 桶、桶内 FIFO（与原先整表分桶后遍历次序一致）。
/// 入队时直接写入对应桶；与双队列配合时，步骤 9 可对调 current/next 引用并清空新的 next，避免 O(n) 拷贝。
/// </summary>
internal sealed class AbilityQueueBuckets
{
    public const int BucketCount = 6;

    private readonly List<AbilityQueueEntry>[] _buckets;

    public AbilityQueueBuckets()
    {
        _buckets = new List<AbilityQueueEntry>[BucketCount];
        for (int i = 0; i < BucketCount; i++)
            _buckets[i] = new List<AbilityQueueEntry>(4);
    }

    public static int BucketIndex(AbilityPriority p)
    {
        int b = (int)p;
        if ((uint)b >= (uint)BucketCount)
            b = (int)AbilityPriority.Medium;
        return b;
    }

    public List<AbilityQueueEntry> Bucket(int index) => _buckets[index];

    public void AddToBucket(int bucketIndex, AbilityQueueEntry entry) => _buckets[bucketIndex].Add(entry);

    public void Clear()
    {
        for (int i = 0; i < BucketCount; i++)
            _buckets[i].Clear();
    }

    /// <summary>全部桶均无条目。</summary>
    public bool IsEmpty
    {
        get
        {
            for (int i = 0; i < BucketCount; i++)
            {
                if (_buckets[i].Count > 0)
                    return false;
            }
            return true;
        }
    }

    /// <summary>在全部桶中自尾向前查找同 (Owner, AbilityIndex) 且无 InvokeTarget 的条目并累加 PendingCount；找到则 true。</summary>
    public bool TryMergePending(ItemState owner, int abilityIdx, int pendingToAdd)
    {
        for (int bi = 0; bi < BucketCount; bi++)
        {
            var list = _buckets[bi];
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (e.Owner == owner && e.AbilityIndex == abilityIdx && e.InvokeTargetSideIndex == null)
                {
                    e.PendingCount += pendingToAdd;
                    return true;
                }
            }
        }
        return false;
    }
}
