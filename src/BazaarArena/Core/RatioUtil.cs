namespace BazaarArena.Core;

/// <summary>比例转化：向下取整、至少为 1；以及按百分比的数值计算（如减免量）。</summary>
public static class RatioUtil
{
    /// <summary>计算 value 的 percent%（0–100），向下取整；用于如「减免量」「扣除量」等，结果可为 0。</summary>
    public static int PercentOf(int value, int percent)
    {
        if (value <= 0 || percent <= 0) return 0;
        return value * Math.Clamp(percent, 0, 100) / 100;
    }

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

    /// <summary>对 valueFormula 求值结果按 percentFormula 求值得到的百分比向下取整，返回新公式（percent 可来自字段或公式）。</summary>
    public static Formula PercentFloor(Formula valueFormula, Formula percentFormula) =>
        Formula.Apply(valueFormula, percentFormula, PercentFloor);
}
