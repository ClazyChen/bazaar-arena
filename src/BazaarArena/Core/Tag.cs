namespace BazaarArena.Core;

/// <summary>物品标签常量，用于 Tags 列表，避免魔法字符串。</summary>
public static class Tag
{
    public const string Weapon = "武器";
    public const string Tool = "工具";
    public const string Apparel = "服饰";
    public const string Friend = "伙伴";
    public const string Food = "食物";
    public const string Tech = "科技";
    /// <summary>地产。</summary>
    public const string Property = "地产";
    /// <summary>载具。</summary>
    public const string Vehicle = "载具";
    /// <summary>遗物。</summary>
    public const string Relic = "遗物";
    /// <summary>巨龙。</summary>
    public const string Dragon = "巨龙";
    /// <summary>无人机。</summary>
    public const string Drone = "无人机";
    /// <summary>玩具。</summary>
    public const string Toy = "玩具";
    /// <summary>水系。</summary>
    public const string Aquatic = "水系";
    /// <summary>射线。</summary>
    public const string Ray = "射线";

    /// <summary>小型（由注册时按 Size 自动添加）。</summary>
    public const string Small = "小型";
    /// <summary>中型（由注册时按 Size 自动添加）。</summary>
    public const string Medium = "中型";
    /// <summary>大型（由注册时按 Size 自动添加）。</summary>
    public const string Large = "大型";

    // 效果类型标签：用于判断护盾/伤害/灼烧等物品，供 Condition 与可暴击判定使用
    /// <summary>护盾。</summary>
    public const string Shield = "护盾";
    /// <summary>伤害。</summary>
    public const string Damage = "伤害";
    /// <summary>弹药（由注册时 AmmoCap &gt; 0 自动添加）。</summary>
    public const string Ammo = "弹药";
    /// <summary>灼烧。</summary>
    public const string Burn = "灼烧";
    /// <summary>剧毒。</summary>
    public const string Poison = "剧毒";
    /// <summary>治疗。</summary>
    public const string Heal = "治疗";
    /// <summary>生命再生。</summary>
    public const string Regen = "再生";

    /// <summary>可暴击（由注册时根据 UseItem+UseSelf+ApplyCritMultiplier 与六类效果 Tag 自动添加）。</summary>
    public const string Crit = "暴击";

    /// <summary>有冷却（由注册时 CooldownMs &gt; 0 自动添加）。</summary>
    public const string Cooldown = "冷却";

    // 机制标签：用于协同先验与搜索启发式（由注册时根据 Ability.Apply 自动添加）
    /// <summary>充能。</summary>
    public const string Charge = "充能";
    /// <summary>冻结。</summary>
    public const string Freeze = "冻结";
    /// <summary>减速。</summary>
    public const string Slow = "减速";
    /// <summary>加速。</summary>
    public const string Haste = "加速";
    /// <summary>装填。</summary>
    public const string Reload = "装填";
    /// <summary>修复。</summary>
    public const string Repair = "修复";
    /// <summary>摧毁。</summary>
    public const string Destroy = "摧毁";
    /// <summary>开始飞行。</summary>
    public const string StartFlying = "开始飞行";
    /// <summary>结束飞行。</summary>
    public const string StopFlying = "结束飞行";
}
