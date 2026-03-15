using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>单方玩家战斗状态：生命、护盾、灼烧、剧毒、生命再生、金币与物品列表。数值字段存于字典，可通过名字访问。</summary>
public class BattleSide
{
    /// <summary>阵营/生命/护盾等数值键，用于按名解析（如公式、日志）。</summary>
    public const string KeySideIndex = "SideIndex";
    public const string KeyMaxHp = "MaxHp";
    public const string KeyHp = "Hp";
    public const string KeyShield = "Shield";
    public const string KeyBurn = "Burn";
    public const string KeyPoison = "Poison";
    public const string KeyRegen = "Regen";
    /// <summary>金币数量，默认 0；后续可能影响战斗或结算。</summary>
    public const string KeyGold = "Gold";

    private readonly Dictionary<string, int> _values = [];

    /// <summary>按键读取数值，不存在则返回默认值。</summary>
    public int GetInt(string key, int defaultValue = 0) =>
        _values.TryGetValue(key, out var v) ? v : defaultValue;

    /// <summary>按键写入数值。</summary>
    public void SetInt(string key, int value) => _values[key] = value;

    /// <summary>本侧在本次战斗中的阵营下标（0 或 1），由 Run 初始化时设置。</summary>
    public int SideIndex { get => GetInt(KeySideIndex, 0); set => SetInt(KeySideIndex, value); }
    public int MaxHp { get => GetInt(KeyMaxHp, 0); set => SetInt(KeyMaxHp, value); }
    public int Hp { get => GetInt(KeyHp, 0); set => SetInt(KeyHp, value); }
    public int Shield { get => GetInt(KeyShield, 0); set => SetInt(KeyShield, value); }
    public int Burn { get => GetInt(KeyBurn, 0); set => SetInt(KeyBurn, value); }
    public int Poison { get => GetInt(KeyPoison, 0); set => SetInt(KeyPoison, value); }
    public int Regen { get => GetInt(KeyRegen, 0); set => SetInt(KeyRegen, value); }
    /// <summary>本侧金币数量，默认 0。</summary>
    public int Gold { get => GetInt(KeyGold, 0); set => SetInt(KeyGold, value); }

    public List<BattleItemState> Items { get; set; } = [];

    /// <summary>战斗内按光环上下文读取物品属性；凡有 (side, item) 的战斗内读属性应走此入口。</summary>
    public int GetItemInt(int itemIndex, string key, int defaultValue = 0)
    {
        var item = Items[itemIndex];
        return item.Template.GetInt(key, item.Tier, defaultValue, new BattleAuraContext(this, item));
    }
}
