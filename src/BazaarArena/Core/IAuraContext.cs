namespace BazaarArena.Core;

/// <summary>战斗内光环上下文：在读取属性时由模拟器提供，用于在 GetInt 内叠加己方光环效果。仅战斗内使用，局外/UI 不传。</summary>
public interface IAuraContext
{
    /// <summary>获取当前属性名对应的光环固定加成之和与百分比加成之和（百分比为整数，如 10 表示 +10%）。</summary>
    void GetAuraModifiers(string attributeName, out int fixedSum, out int percentSum);
}
