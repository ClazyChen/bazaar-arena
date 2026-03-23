using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>单方玩家战斗状态：生命、护盾、灼烧、剧毒、生命再生、金币与物品列表；数值存于 <see cref="Attributes"/>，与 <see cref="Key"/> 对齐。</summary>
public class BattleSide
{
    /// <summary>阵营数值（下标 0～7 对应 Key.SideIndex～Key.Gold）。</summary>
    public int[] Attributes { get; } = new int[Key.SideStateAttributeCount];

    public int GetAttribute(int key) =>
        (uint)key < (uint)Attributes.Length ? Attributes[key] : 0;

    public void SetAttribute(int key, int value)
    {
        if ((uint)key < (uint)Attributes.Length)
            Attributes[key] = value;
    }

    /// <summary>按键名解析（如卡组 JSON、公式字符串）；未知名返回默认值。</summary>
    public int GetInt(string keyName, int defaultValue = 0) =>
        Key.TryGetKey(keyName, out int k) ? GetAttribute(k) : defaultValue;

    public void SetInt(string keyName, int value)
    {
        if (Key.TryGetKey(keyName, out int k))
            SetAttribute(k, value);
    }

    public int SideIndex { get => Attributes[Key.SideIndex]; set => Attributes[Key.SideIndex] = value; }
    public int MaxHp { get => Attributes[Key.Damage]; set => Attributes[Key.Damage] = value; }
    public int Hp { get => Attributes[Key.Heal]; set => Attributes[Key.Heal] = value; }
    public int Shield { get => Attributes[Key.Shield]; set => Attributes[Key.Shield] = value; }
    public int Burn { get => Attributes[Key.Burn]; set => Attributes[Key.Burn] = value; }
    public int Poison { get => Attributes[Key.Poison]; set => Attributes[Key.Poison] = value; }
    public int Regen { get => Attributes[Key.Regen]; set => Attributes[Key.Regen] = value; }
    public int Gold { get => Attributes[Key.Gold]; set => Attributes[Key.Gold] = value; }

    public List<ItemState> Items { get; set; } = [];

    /// <summary>战斗内读物品属性：下标在 ItemState 范围内走运行时数组，否则走模板。</summary>
    public int GetItemInt(int itemIndex, int key, int defaultValue = 0)
    {
        var item = Items[itemIndex];
        if ((uint)key < (uint)item.Attributes.Length)
            return item.GetAttribute(key);
        return item.Template.GetInt(key, item.Tier, defaultValue);
    }
}
