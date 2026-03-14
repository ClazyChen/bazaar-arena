using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>战斗内光环上下文：持有己方与目标物品，在 GetAuraModifiers 中遍历己方未摧毁物品的光环并累加。可选传入敌方阵营供公式（如 OpponentPoison）使用。</summary>
internal sealed class BattleAuraContext(BattleSide side, BattleItemState targetItem, BattleSide? opp = null) : IAuraContext
{
    public void GetAuraModifiers(string attributeName, out int fixedSum, out int percentSum)
    {
        fixedSum = 0;
        percentSum = 0;
        for (int i = 0; i < side.Items.Count; i++)
        {
            var source = side.Items[i];
            if (source.Destroyed) continue;
            foreach (var aura in source.Template.Auras)
            {
                if (aura.AttributeName != attributeName) continue;
                // 评估光环条件时仅用模板标签，避免 GrantedTags 产生循环
                IReadOnlySet<string> TemplateTagsOnly(BattleItemState i) =>
                    (i.Template.Tags != null && i.Template.Tags.Count > 0) ? new HashSet<string>(i.Template.Tags) : [];
                var auraCtx = new ConditionContext
                {
                    MySide = side,
                    EnemySide = opp ?? side,
                    Item = targetItem,
                    Source = source,
                    GetEffectiveTagsForItem = TemplateTagsOnly,
                };
                if (aura.Condition != null && !aura.Condition.Evaluate(auraCtx)) continue;
                if (aura.SourceCondition != null)
                {
                    var sourceOnlyCtx = new ConditionContext
                    {
                        MySide = side,
                        EnemySide = opp ?? side,
                        Item = source,
                        Source = source,
                        GetEffectiveTagsForItem = TemplateTagsOnly,
                    };
                    if (!aura.SourceCondition.Evaluate(sourceOnlyCtx)) continue;
                }
                if (aura.Value != null)
                {
                    int v = aura.Value.Evaluate(new FormulaContext(source, side, opp));
                    if (aura.Percent)
                        percentSum += v;
                    else
                        fixedSum += v;
                }
            }
        }
    }
}
