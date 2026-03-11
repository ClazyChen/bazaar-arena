using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>战斗内光环上下文：持有己方与目标物品下标，在 GetAuraModifiers 中遍历己方未摧毁物品的光环并累加。可选传入敌方阵营供公式（如 OpponentPoison）使用。</summary>
internal sealed class BattleAuraContext(BattleSide side, int targetItemIndex, BattleSide? opp = null) : IAuraContext
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
                var targetItem = side.Items[targetItemIndex];
                var auraCtx = new ConditionContext
                {
                    MySide = side,
                    EnemySide = opp ?? side,
                    Item = targetItem,
                    Source = source,
                };
                if (aura.Condition != null && !aura.Condition.Evaluate(auraCtx)) continue;
                if (!string.IsNullOrEmpty(aura.FixedValueFormula))
                    fixedSum += AuraFormulaEvaluator.Evaluate(aura.FixedValueFormula, source, i, side, opp);
                else if (!string.IsNullOrEmpty(aura.FixedValueKey))
                    fixedSum += source.Template.GetInt(aura.FixedValueKey, source.Tier, 0, new BattleAuraContext(side, i));
                if (!string.IsNullOrEmpty(aura.PercentValueKey))
                    percentSum += source.Template.GetInt(aura.PercentValueKey, source.Tier, 0, new BattleAuraContext(side, i));
            }
        }
    }
}
