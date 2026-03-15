namespace BazaarArena.Core;

/// <summary>英雄标识常量，表示物品所属英雄；用于 ItemTemplate.Hero，避免魔法字符串。</summary>
public static class Hero
{
    /// <summary>通用（当前版本所有物品默认）。</summary>
    public const string Common = "Common";

    /// <summary>海盗 Vanessa 关卡专属物品。</summary>
    public const string Vanessa = "Vanessa";
}
