using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>
/// 能力队列按 <see cref="AbilityPriority"/> 分 6 桶、桶内 FIFO（与原先整表分桶后遍历次序一致）。
/// 入队时直接写入对应桶；与双队列配合时，步骤 9 可对调 current/next 引用并清空新的 next，避免 O(n) 拷贝。
/// </summary>
internal sealed class AbilityQueueBuckets
{
    public const int BucketCount = 6;

    private readonly List<RuntimeAbilityRef>[] _buckets;

    public AbilityQueueBuckets()
    {
        _buckets = new List<RuntimeAbilityRef>[BucketCount];
        for (int i = 0; i < BucketCount; i++)
            _buckets[i] = new List<RuntimeAbilityRef>(8);
    }

    public static int BucketIndex(AbilityPriority p)
    {
        int b = (int)p;
        if ((uint)b >= (uint)BucketCount)
            b = (int)AbilityPriority.Medium;
        return b;
    }

    public List<RuntimeAbilityRef> Bucket(int index) => _buckets[index];

    public void AddToBucket(int bucketIndex, RuntimeAbilityRef abilityRef) => _buckets[bucketIndex].Add(abilityRef);

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

}
