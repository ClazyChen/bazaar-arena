using BazaarArena.Core;
using BazaarArena.ItemDatabase;

namespace BazaarArena.QualityDeckFinder;

/// <summary>基于物品声明的上游/下游/邻居协同先验，计算候选物品在给定槽位上的协同得分。</summary>
public static class SynergyScorer
{
    /// <summary>
    /// 计算将候选物品 X 放在 deck 的 slotIndex 槽位时的协同得分。
    /// 得分 = 满足的上游子句数 + 满足的下游子句数 + (左邻满足任一条则 +1) + (右邻满足任一条则 +1)。
    /// </summary>
    public static double Score(ItemTemplate? candidateX, DeckRep deck, int slotIndex, IItemTemplateResolver db)
    {
        if (candidateX == null || slotIndex < 0 || slotIndex >= deck.ItemNames.Count)
            return 0.0;

        double score = 0.0;
        var names = deck.ItemNames;
        int n = names.Count;

        if (candidateX.UpstreamRequirements != null && candidateX.UpstreamRequirements.Count > 0)
        {
            foreach (var clause in candidateX.UpstreamRequirements)
            {
                if (HasItemSatisfyingInDirection(names, slotIndex, clause, true, db))
                    score += 1.0;
            }
        }

        if (candidateX.DownstreamRequirements != null && candidateX.DownstreamRequirements.Count > 0)
        {
            foreach (var clause in candidateX.DownstreamRequirements)
            {
                if (HasItemSatisfyingInDirection(names, slotIndex, clause, false, db))
                    score += 1.0;
            }
        }

        if (candidateX.NeighborPreference != null && candidateX.NeighborPreference.Count > 0)
        {
            var leftIdx = slotIndex - 1;
            var rightIdx = slotIndex + 1;
            if (leftIdx >= 0 && SatisfiesAnyClause(db.GetTemplate(names[leftIdx]), candidateX.NeighborPreference))
                score += 1.0;
            if (rightIdx < n && SatisfiesAnyClause(db.GetTemplate(names[rightIdx]), candidateX.NeighborPreference))
                score += 1.0;
        }

        return score;
    }

    /// <summary>在逐槽生成卡组时，候选物品放在 slotIndex（左侧槽位已由 namesFilled 填满）时的协同得分。仅考虑左侧与左邻。</summary>
    public static double ScoreForBuilding(IReadOnlyList<string> namesFilled, int slotIndex, string candidateItemName, IItemTemplateResolver db)
    {
        var templateX = db.GetTemplate(candidateItemName);
        if (templateX == null || slotIndex < 0) return 0.0;
        int n = namesFilled.Count;
        double score = 0.0;

        if (templateX.UpstreamRequirements != null)
        {
            foreach (var clause in templateX.UpstreamRequirements)
            {
                if (clause.Direction == SynergyDirection.Right) continue;
                for (int j = 0; j < n; j++)
                {
                    if (clause.Direction == SynergyDirection.Left && j >= slotIndex) continue;
                    var other = db.GetTemplate(namesFilled[j]);
                    if (SatisfiesClause(other, clause)) { score += 1.0; break; }
                }
            }
        }
        if (templateX.DownstreamRequirements != null)
        {
            foreach (var clause in templateX.DownstreamRequirements)
            {
                if (clause.Direction == SynergyDirection.Right) continue;
                for (int j = 0; j < n; j++)
                {
                    if (clause.Direction == SynergyDirection.Left && j >= slotIndex) continue;
                    var other = db.GetTemplate(namesFilled[j]);
                    if (SatisfiesClause(other, clause)) { score += 1.0; break; }
                }
            }
        }
        if (templateX.NeighborPreference != null && slotIndex > 0)
        {
            var leftName = namesFilled[slotIndex - 1];
            if (SatisfiesAnyClause(db.GetTemplate(leftName), templateX.NeighborPreference))
                score += 1.0;
        }
        return score;
    }

    /// <summary>对当前卡组顺序打分：各物品的上游/下游/邻居满足子句数之和（用于排列 tie-break 或代表选择）。</summary>
    public static double DeckOrderScore(DeckRep deck, IItemTemplateResolver db)
    {
        double total = 0.0;
        var names = deck.ItemNames;
        for (int i = 0; i < names.Count; i++)
        {
            var t = db.GetTemplate(names[i]);
            if (t == null) continue;
            total += Score(t, deck, i, db);
        }
        return total;
    }

    private static bool HasItemSatisfyingInDirection(
        IReadOnlyList<string> names,
        int slotIndex,
        SynergyClause clause,
        bool upstreamElseDownstream,
        IItemTemplateResolver db)
    {
        for (int j = 0; j < names.Count; j++)
        {
            if (j == slotIndex) continue;
            bool leftOfSlot = j < slotIndex;
            bool rightOfSlot = j > slotIndex;
            if (clause.Direction == SynergyDirection.Left && !leftOfSlot) continue;
            if (clause.Direction == SynergyDirection.Right && !rightOfSlot) continue;
            var other = db.GetTemplate(names[j]);
            if (SatisfiesClause(other, clause))
                return true;
        }
        return false;
    }

    private static bool SatisfiesClause(ItemTemplate? t, SynergyClause clause)
    {
        if (t?.Tags == null || clause.Tags == null || clause.Tags.Count == 0)
            return false;
        return clause.Tags.All(tag => t.Tags.Contains(tag));
    }

    private static bool SatisfiesAnyClause(ItemTemplate? t, List<SynergyClause> clauses)
    {
        if (t == null || clauses == null) return false;
        return clauses.Any(c => SatisfiesClause(t, c));
    }
}
