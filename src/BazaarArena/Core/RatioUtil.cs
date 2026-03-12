namespace BazaarArena.Core;

/// <summary>比例转化：向下取整、至少为 1。例如 1–39 的 5% 均为 1。</summary>
public static class RatioUtil
{
    /// <summary>计算 value 的 percent%（0–100），向下取整，结果至少为 1（若 value 至少为 1）。</summary>
    public static int PercentFloor(int value, int percent)
    {
        if (value <= 0) return 0;
        int result = value * percent / 100;
        return result < 1 ? 1 : result;
    }

    /// <summary>对公式求值结果再按 percent% 向下取整，返回新公式（用于光环等）。</summary>
    public static Formula PercentFloor(Formula valueFormula, int percent) =>
        Formula.Apply(valueFormula, v => PercentFloor(v, percent));
}
