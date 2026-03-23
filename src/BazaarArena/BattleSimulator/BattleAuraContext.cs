using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>战斗内光环上下文：持有己方与目标物品，在 GetAuraModifiers 中遍历己方未摧毁物品的光环并累加。可选传入敌方阵营供公式（如 OpponentPoison）使用。</summary>
internal sealed class BattleAuraContext(BattleSide side, BattleItemState targetItem, BattleSide? opp = null) : IAuraContext
{
    public void GetAuraModifiers(string attributeName, out int fixedSum, out int percentSum)
    {
        fixedSum = 0;
        percentSum = 0;
        var battleState = new BattleState { Side0 = side, Side1 = opp ?? side };
        int attributeKey = int.TryParse(attributeName, out var parsed) ? parsed : -1;
        for (int i = 0; i < side.Items.Count; i++)
        {
            var source = side.Items[i];
            if (source.Destroyed) continue;
            foreach (var aura in source.Template.Auras)
            {
                if (aura.Attribute != attributeKey) continue;
                var auraCtx = new BattleContext
                {
                    Item = targetItem,
                    Source = source,
                    Caster = source,
                    BattleState = battleState,
                };
                if (aura.Condition.Evaluate(auraCtx) == 0) continue;
                if (aura.SourceCondition != null)
                {
                    var sourceOnlyCtx = new BattleContext
                    {
                        Item = source,
                        Source = source,
                        Caster = source,
                        BattleState = battleState,
                    };
                    if (aura.SourceCondition.Evaluate(sourceOnlyCtx) == 0) continue;
                }
                if (aura.Value != null)
                {
                    int v = aura.Value.Evaluate(new BattleContext
                    {
                        BattleState = battleState,
                        Item = targetItem,
                        Source = source,
                        Caster = source,
                    });
                    if (aura.Percent)
                        percentSum += v;
                    else
                        fixedSum += v;
                }
            }
        }
    }
}
