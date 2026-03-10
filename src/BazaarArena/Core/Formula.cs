namespace BazaarArena.Core;

/// <summary>光环固定加成公式名常量，用于 <see cref="AuraDefinition.FixedValueFormula"/>，避免魔法字符串。</summary>
public static class Formula
{
    /// <summary>固定加成 = Custom_0 × (己方未摧毁小型物品数 + StashParameter)。</summary>
    public const string SmallCountStash = "SmallCountStash";
    /// <summary>固定加成 = 敌方当前剧毒值（用于如灵质：获得治疗等量于敌人剧毒）。</summary>
    public const string OpponentPoison = "OpponentPoison";
    /// <summary>固定加成 = 光环来源物品的 Damage（含光环），用于如「Burn += 自身 Damage」。</summary>
    public const string SourceDamage = "SourceDamage";
}
