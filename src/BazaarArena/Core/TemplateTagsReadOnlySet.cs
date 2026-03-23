using System.Collections;
using System.Runtime.CompilerServices;

namespace BazaarArena.Core;

/// <summary>
/// 将物品模板的 <see cref="List{T}"/> 标签暴露为 <see cref="IReadOnlySet{T}"/>，避免条件评估时反复 <c>new HashSet</c> 拷贝。
/// 模板侧列表在战斗内只读，故可安全共享视图。
/// </summary>
internal static class TemplateTagsReadOnlySet
{
    private static int AsEnumerableSetCount(IEnumerable<string> e) =>
        e is IReadOnlySet<string> r ? r.Count : e.ToHashSet().Count;

    private static readonly EmptyReadOnlyStringSet Empty = new();

    private sealed class EmptyReadOnlyStringSet : IReadOnlySet<string>
    {
        public int Count => 0;
        public bool Contains(string item) => false;
        public IEnumerator<string> GetEnumerator() => Enumerable.Empty<string>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool IsProperSubsetOf(IReadOnlySet<string> other) => other.Count > 0;
        public bool IsProperSupersetOf(IReadOnlySet<string> other) => false;
        public bool IsSubsetOf(IReadOnlySet<string> other) => true;
        public bool IsSupersetOf(IReadOnlySet<string> other) => other.Count == 0;
        public bool Overlaps(IReadOnlySet<string> other) => false;
        public bool SetEquals(IReadOnlySet<string> other) => other.Count == 0;

        public bool IsProperSubsetOf(IEnumerable<string> other) => AsEnumerableSetCount(other) > 0;
        public bool IsProperSupersetOf(IEnumerable<string> other) => false;
        public bool IsSubsetOf(IEnumerable<string> other) => true;

        public bool IsSupersetOf(IEnumerable<string> other) => AsEnumerableSetCount(other) == 0;

        public bool Overlaps(IEnumerable<string> other) => false;

        public bool SetEquals(IEnumerable<string> other) => AsEnumerableSetCount(other) == 0;
    }

    private sealed class ListBackedSet(List<string> list) : IReadOnlySet<string>
    {
        private readonly List<string> _list = list;

        public int Count => _list.Count;

        public bool Contains(string item) => _list.Contains(item);

        public IEnumerator<string> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool IsProperSubsetOf(IReadOnlySet<string> other)
        {
            if (_list.Count == 0) return other.Count > 0;
            if (_list.Count >= other.Count) return false;
            return IsSubsetOf(other) && _list.Count < other.Count;
        }

        public bool IsProperSupersetOf(IReadOnlySet<string> other)
        {
            if (other.Count == 0) return _list.Count > 0;
            if (_list.Count <= other.Count) return false;
            return IsSupersetOf(other);
        }

        public bool IsSubsetOf(IReadOnlySet<string> other)
        {
            foreach (var x in _list)
            {
                if (!other.Contains(x)) return false;
            }
            return true;
        }

        public bool IsSupersetOf(IReadOnlySet<string> other)
        {
            foreach (var x in other)
            {
                if (!_list.Contains(x)) return false;
            }
            return true;
        }

        public bool Overlaps(IReadOnlySet<string> other)
        {
            foreach (var x in _list)
            {
                if (other.Contains(x)) return true;
            }
            return false;
        }

        public bool SetEquals(IReadOnlySet<string> other)
        {
            if (_list.Count != other.Count) return false;
            return IsSubsetOf(other);
        }

        public bool IsProperSubsetOf(IEnumerable<string> other) =>
            other is IReadOnlySet<string> ros ? IsProperSubsetOf(ros) : IsProperSubsetOf(other.ToHashSet());

        public bool IsProperSupersetOf(IEnumerable<string> other) =>
            other is IReadOnlySet<string> ros ? IsProperSupersetOf(ros) : IsProperSupersetOf(other.ToHashSet());

        public bool IsSubsetOf(IEnumerable<string> other) =>
            other is IReadOnlySet<string> ros ? IsSubsetOf(ros) : IsSubsetOf(other.ToHashSet());

        public bool IsSupersetOf(IEnumerable<string> other) =>
            other is IReadOnlySet<string> ros ? IsSupersetOf(ros) : IsSupersetOf(other.ToHashSet());

        public bool Overlaps(IEnumerable<string> other) =>
            other is IReadOnlySet<string> ros ? Overlaps(ros) : Overlaps(other.ToHashSet());

        public bool SetEquals(IEnumerable<string> other) =>
            other is IReadOnlySet<string> ros ? SetEquals(ros) : SetEquals(other.ToHashSet());
    }

    private static readonly ConditionalWeakTable<List<string>, ListBackedSet> s_views = new();

    /// <summary>模板标签列表的只读集合视图；null 或空列表返回共享空集。</summary>
    public static IReadOnlySet<string> ForList(List<string>? tags)
    {
        if (tags == null || tags.Count == 0)
            return Empty;
        return s_views.GetValue(tags, static list => new ListBackedSet(list));
    }
}
