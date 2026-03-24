using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>计算物品在战斗内的有效标签（模板标签 + Key.Tags 光环位掩码授予，如「相邻视为载具」）。评估光环条件时仅用模板标签以避免循环。</summary>
internal static class EffectiveTagHelper
{
    [ThreadStatic] private static HashSet<int>? t_effectiveTagScratch;

    /// <summary>计算指定物品的有效标签集合。遍历双方所有未摧毁物品的 Key.Tags 光环，若条件成立则将 Value 计算出的标签掩码并入结果。</summary>
    /// <remarks>返回的集合为线程内可复用缓冲区，调用方须同步使用、勿跨 await 缓存引用（与原先每次 new HashSet 的用法一致）。</remarks>
    public static HashSet<int> GetEffectiveTags(BattleSide side0, BattleSide side1, ItemState item)
    {
        var result = t_effectiveTagScratch ??= new HashSet<int>(24);
        result.Clear();
        foreach (var tag in item.Template.Tags.ToList())
        {
            result.Add(tag);
        }

        BattleSide mySide = item.SideIndex == side0.SideIndex ? side0 : side1;
        BattleSide enemySide = item.SideIndex == side0.SideIndex ? side1 : side0;

        ScanAurasForItem(side0, side1, mySide, enemySide, item, result);
        ScanAurasForItem(side1, side0, mySide, enemySide, item, result);

        return result;
    }

    private static BattleContext BuildAuraContext(BattleSide side0, BattleSide side1, ItemState item, ItemState source)
    {
        var state = new BattleState();
        state.Side[0] = side0;
        state.Side[1] = side1;
        return new BattleContext
        {
            BattleState = state,
            Item = item,
            Caster = source,
            Source = source,
            InvokeTarget = null,
        };
    }

    private static void ScanAurasForItem(BattleSide side, BattleSide otherSide, BattleSide mySide, BattleSide enemySide, ItemState item, HashSet<int> result)
    {
        for (int j = 0; j < side.Items.Count; j++)
        {
            var source = side.Items[j];
            if (source.Destroyed) continue;
            foreach (var aura in source.Template.Auras)
            {
                if (aura.Attribute != Key.Tags || aura.Percent || aura.Value == null) continue;
                var auraCtx = BuildAuraContext(mySide, enemySide, item, source);
                if (aura.Condition != null && aura.Condition.Evaluate(auraCtx) == 0) continue;
                int mask = aura.Value.Evaluate(auraCtx);
                int bit = 1;
                while (mask != 0)
                {
                    if ((mask & 1) != 0) result.Add(bit);
                    mask >>= 1;
                    bit <<= 1;
                }
            }
        }
    }
}
