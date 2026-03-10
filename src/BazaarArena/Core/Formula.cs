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
    /// <summary>固定加成 = 己方伙伴数 × 来源的 Custom_0（用于如纳米机器人：每拥有一位伙伴造成 N 伤害）。</summary>
    public const string CompanionCountTimesCustom0 = "CompanionCountTimesCustom0";
    /// <summary>固定加成 = -(相邻且为伙伴的物品数) × 1000（毫秒），用于缩短冷却。</summary>
    public const string Minus1sPerAdjacentCompanion = "Minus1sPerAdjacentCompanion";
    /// <summary>固定加成 = 若己方唯一伙伴即为来源则返回来源的 Custom_0，否则 0（用于如友好玩偶：唯一伙伴时暴击率加成）。</summary>
    public const string OnlyCompanionCritBonus = "OnlyCompanionCritBonus";
}
