namespace BazaarArena.Core;

/// <summary>比例转化：向下取整、至少为 1。例如 1–39 的 5% 均为 1。</summary>
public static class RatioUtil
{
    /// <summary>计算 value 的 percent%（0–100），向下取整，结果至少为 1（若 value 至少为 1）。</summary>
    public static int PercentFloor(int value, int percent)
    {
        if (value <= 0) return 0;
        int result = (value * percent) / 100;
        return result < 1 ? 1 : result;
    }
}
