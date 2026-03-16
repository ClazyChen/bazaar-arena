namespace BazaarArena.QualityDeckFinder;

/// <summary>同形状内邻域：同尺寸替换 + 同尺寸交换（无重复）。</summary>
public static class Neighborhood
{
    /// <summary>枚举所有邻居：同尺寸替换（每槽换同尺寸池中未出现的其它物品）+ 同尺寸交换（同尺寸两槽交换）。</summary>
    public static List<DeckRep> EnumerateNeighbors(DeckRep deck, ItemPool pool)
    {
        var list = new List<DeckRep>();
        var shape = deck.Shape;
        var names = deck.ItemNames.ToList();
        var used = new HashSet<string>(deck.ItemNames, StringComparer.Ordinal);

        for (int slot = 0; slot < shape.Count; slot++)
        {
            var size = shape[slot];
            var current = names[slot];
            foreach (var name in pool.NamesForSize(size))
            {
                if (name == current) continue;
                if (used.Contains(name)) continue;
                var next = new List<string>(names) { [slot] = name };
                list.Add(new DeckRep(shape, next));
            }
        }

        for (int i = 0; i < shape.Count; i++)
        for (int j = i + 1; j < shape.Count; j++)
        {
            if (shape[i] != shape[j]) continue;
            var next = new List<string>(names);
            (next[i], next[j]) = (next[j], next[i]);
            list.Add(new DeckRep(shape, next));
        }

        return list;
    }

    /// <summary>随机采样最多 sampleSize 个邻居，不重复。</summary>
    public static List<DeckRep> SampleNeighbors(DeckRep deck, ItemPool pool, Random rng, int sampleSize)
    {
        var all = EnumerateNeighbors(deck, pool);
        if (all.Count <= sampleSize) return all;
        var indices = Enumerable.Range(0, all.Count).OrderBy(_ => rng.Next()).Take(sampleSize).ToList();
        return indices.Select(i => all[i]).ToList();
    }
}
