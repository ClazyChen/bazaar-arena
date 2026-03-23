using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>过渡中的战斗侧公式辅助上下文。</summary>
internal sealed class FormulaContext(BattleItemState source, BattleSide side, BattleSide? opp)
{
    public int GetSourceInt(string key) =>
        source.Template.GetInt(key, source.Tier, 0);

    public int GetSideInt(string key) => side.GetInt(key, 0);
    public int GetOppInt(string key) => opp?.GetInt(key, 0) ?? 0;

    public int GetSideItemMax(string key)
    {
        int max = 0;
        foreach (var item in side.Items)
        {
            if (item.Destroyed) continue;
            int v = item.Template.GetInt(key, item.Tier, 0);
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
            int v = item.Template.GetInt(key, item.Tier, 0);
            if (!any) { min = v; any = true; }
            else if (v < min) min = v;
        }
        return any ? min : 0;
    }

    public int Count(Formula? condition)
    {
        if (condition == null) return 0;
        int n = 0;
        foreach (var item in side.Items)
        {
            if (item.Destroyed) continue;
            var ctx = new BattleContext
            {
                BattleState = new BattleState(),
                Item = item,
                Caster = source,
                Source = source,
            };
            if (condition.Evaluate(ctx) != 0) n++;
        }
        if (opp != null)
        {
            foreach (var item in opp.Items)
            {
                if (item.Destroyed) continue;
                var ctx = new BattleContext
                {
                    BattleState = new BattleState(),
                    Item = item,
                    Caster = source,
                    Source = source,
                };
                if (condition.Evaluate(ctx) != 0) n++;
            }
        }
        return n;
    }
}
