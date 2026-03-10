namespace BazaarArena.Core;

/// <summary>光环固定加成公式名常量，用于 <see cref="AuraDefinition.FixedValueFormula"/>，避免魔法字符串。</summary>
public static class Formula
{
    /// <summary>固定加成 = Custom_0 × (己方未摧毁小型物品数 + StashParameter)。</summary>
    public const string SmallCountStash = "SmallCountStash";
}
