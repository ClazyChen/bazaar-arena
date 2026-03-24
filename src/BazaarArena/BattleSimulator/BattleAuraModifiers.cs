using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>战斗内光环：遍历己方未摧毁物品的模板 Auras，累加对某属性的固定/百分比加成。可选传入敌方阵营供公式使用。</summary>
internal static class BattleAuraModifiers
{
    /// <param name="attributeName">属性名或整型 key 的字符串形式（与 AuraDefinition.Attribute 一致）。</param>
    public static void Accumulate(BattleSide side, ItemState targetItem, BattleSide? opp,
        string attributeName, out int fixedSum, out int percentSum)
    {
        fixedSum = 0;
        percentSum = 0;
        var battleState = new BattleState();
        battleState.Side[0] = side;
        battleState.Side[1] = opp ?? side;
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
                    Source = targetItem,
                    Caster = source,
                    BattleState = battleState,
                };
                if (aura.Condition.Evaluate(auraCtx) == 0) continue;
                if (aura.Value != null)
                {
                    int v = aura.Value.Evaluate(new BattleContext
                    {
                        BattleState = battleState,
                        Item = targetItem,
                        Source = targetItem,
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
