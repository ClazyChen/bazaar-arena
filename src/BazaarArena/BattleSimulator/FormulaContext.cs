using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>公式求值上下文实现：持有 source/side/opp，实现 IFormulaContext 供 Formula.Evaluate 使用。</summary>
internal sealed class FormulaContext(BattleItemState source, BattleSide side, BattleSide? opp) : IFormulaContext
{
    public int GetSourceInt(string key) =>
        source.Template.GetInt(key, source.Tier, 0, new BattleAuraContext(side, source, opp));

    public int GetSideInt(string key) => side.GetInt(key, 0);
    public int GetOppInt(string key) => opp?.GetInt(key, 0) ?? 0;

    public int GetSideItemMax(string key)
    {
        int max = 0;
        foreach (var item in side.Items)
        {
            if (item.Destroyed) continue;
            int v = item.Template.GetInt(key, item.Tier, 0, new BattleAuraContext(side, item, opp));
            if (v > max) max = v;
        }
        return max;
    }

    public int GetSideItemMin(string key)
    {
        bool any = false;
        int min = 0;
        foreach (var item in side.Items)
        {
            if (item.Destroyed) continue;
            int v = item.Template.GetInt(key, item.Tier, 0, new BattleAuraContext(side, item, opp));
            if (!any) { min = v; any = true; }
            else if (v < min) min = v;
        }
        return any ? min : 0;
    }

    public int Count(Condition? condition)
    {
        if (condition == null) return 0;
        int n = 0;
        foreach (var item in side.Items)
        {
            if (item.Destroyed) continue;
            var ctx = new ConditionContext { MySide = side, EnemySide = opp ?? side, Item = item, Source = source };
            if (condition.Evaluate(ctx)) n++;
        }
        if (opp != null)
        {
            foreach (var item in opp.Items)
            {
                if (item.Destroyed) continue;
                var ctx = new ConditionContext { MySide = opp, EnemySide = side, Item = item, Source = source };
                if (condition.Evaluate(ctx)) n++;
            }
        }
        return n;
    }
}
