namespace BazaarArena.Core;

/// <summary>属性 key 对应的中文名，用于效果日志默认显示（如「护盾降低」「伤害提高」）；未收录时用「属性」。</summary>
public static class AttributeLogNames
{
    private static readonly Dictionary<int, string> Map = new()
    {
        [Key.Damage] = "伤害",
        [Key.Shield] = "护盾",
        [Key.Heal] = "治疗",
        [Key.Burn] = "灼烧",
        [Key.Poison] = "剧毒",
        [Key.CritRate] = "暴击率",
        [Key.Gold] = "金币",
        [Key.InFlight] = "飞行",
        [Key.FreezeRemainingMs] = "冻结",
        [Key.CooldownMs] = "冷却时间",
    };

    /// <summary>获取属性 key 的中文名；未收录时返回「属性」。</summary>
    public static string Get(int attributeKey) => Map.TryGetValue(attributeKey, out var name) ? name : "属性";
}
