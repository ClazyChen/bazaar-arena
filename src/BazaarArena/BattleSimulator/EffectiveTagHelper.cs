using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>计算物品在战斗内的有效标签（模板标签 + 光环授予，如「相邻视为载具」）。评估光环条件时仅用模板标签以避免循环。</summary>
internal static class EffectiveTagHelper
{
    /// <summary>计算指定物品的有效标签集合。遍历双方所有未摧毁物品的光环，若光环有 GrantedTags 且条件（用仅模板标签的上下文）对该 item 成立，则将该光环的 GrantedTags 并入结果。</summary>
    public static HashSet<string> GetEffectiveTags(BattleSide side0, BattleSide side1, BattleItemState item)
    {
        var result = new HashSet<string>(item.Template.Tags ?? []);
        BattleSide mySide = item.SideIndex == side0.SideIndex ? side0 : side1;
        BattleSide enemySide = item.SideIndex == side0.SideIndex ? side1 : side0;
        // 评估光环条件时仅用模板标签，避免「视为载具」等产生循环依赖
        IReadOnlySet<string> TemplateTagsOnly(BattleItemState i)
        {
            var t = i.Template.Tags;
            return t != null && t.Count > 0 ? new HashSet<string>(t) : [];
        }
        foreach (var (side, otherSide) in new[] { (side0, side1), (side1, side0) })
        {
            for (int j = 0; j < side.Items.Count; j++)
            {
                var source = side.Items[j];
                if (source.Destroyed) continue;
                foreach (var aura in source.Template.Auras)
                {
                    if (aura.GrantedTags == null || aura.GrantedTags.Count == 0) continue;
                    var auraCtx = new ConditionContext
                    {
                        MySide = mySide,
                        EnemySide = enemySide,
                        Item = item,
                        Source = source,
                        GetEffectiveTagsForItem = TemplateTagsOnly,
                    };
                    if (aura.Condition != null && !aura.Condition.Evaluate(auraCtx)) continue;
                    if (aura.SourceCondition != null)
                    {
                        var sourceOnlyCtx = new ConditionContext
                        {
                            MySide = side,
                            EnemySide = otherSide,
                            Item = source,
                            Source = source,
                            GetEffectiveTagsForItem = TemplateTagsOnly,
                        };
                        if (!aura.SourceCondition.Evaluate(sourceOnlyCtx)) continue;
                    }
                    foreach (var tag in aura.GrantedTags)
                        result.Add(tag);
                }
            }
        }
        return result;
    }
}
