using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>光环固定加成公式求值：根据 <see cref="AuraDefinition.FixedValueFormula"/> 计算固定加成的数值，避免公式逻辑堆在 <see cref="BattleAuraContext"/>。</summary>
internal static class AuraFormulaEvaluator
{
    /// <summary>根据公式名、施放源物品、来源下标、己方阵营与可选敌方阵营计算固定加成值；未知公式返回 0。sourceIndex 用于依赖来源属性的公式（如 SourceDamage）。</summary>
    public static int Evaluate(string? formulaName, BattleItemState source, int sourceIndex, BattleSide side, BattleSide? opp = null)
    {
        if (string.IsNullOrEmpty(formulaName)) return 0;
        return formulaName switch
        {
            Formula.SmallCountStash => EvaluateSmallCountStash(source, side),
            Formula.OpponentPoison => opp?.Poison ?? 0,
            Formula.SourceDamage => source.Template.GetInt("Damage", source.Tier, 0, new BattleAuraContext(side, sourceIndex)),
            _ => 0,
        };
    }

    private static int EvaluateSmallCountStash(BattleItemState source, BattleSide side)
    {
        int smallCount = 0;
        for (int j = 0; j < side.Items.Count; j++)
        {
            var it = side.Items[j];
            if (!it.Destroyed && it.Template.Size == ItemSize.Small) smallCount++;
        }
        int custom0 = source.Template.GetInt("Custom_0", source.Tier, 0);
        int stash = source.Template.GetInt("StashParameter", source.Tier, 0);
        return custom0 * (smallCount + stash);
    }
}
