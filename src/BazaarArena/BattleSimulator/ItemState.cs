using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>战斗中单件物品的运行时状态（Attribute[Key.xxx]）。</summary>
public class ItemState
{
    public ItemTemplate Template { get; init; }
    public int[] Attributes { get; init; } = new int[Key.ItemStateAttributeCount];

    public int GetAttribute(int key) => Attributes[key];
    public void SetAttribute(int key, int value) => Attributes[key] = value;
    public bool GetBoolAttribute(int key) => Attributes[key] != 0;
    public void SetBoolAttribute(int key, bool value) => Attributes[key] = value ? 1 : 0;

    public int SideIndex { get => Attributes[Key.SideIndex]; set => Attributes[Key.SideIndex] = value; }
    public int ItemIndex { get => Attributes[Key.ItemIndex]; set => Attributes[Key.ItemIndex] = value; }
    public ItemTier Tier { get => (ItemTier)Attributes[Key.Tier]; set => Attributes[Key.Tier] = (int)value; }
    public int ChargedTimeMs { get => Attributes[Key.ChargedTimeMs]; set => Attributes[Key.ChargedTimeMs] = value; }
    public int CooldownElapsedMs { get => Attributes[Key.ChargedTimeMs]; set => Attributes[Key.ChargedTimeMs] = value; }
    public int FreezeRemainingMs { get => Attributes[Key.FreezeRemainingMs]; set => Attributes[Key.FreezeRemainingMs] = value; }
    public int SlowRemainingMs { get => Attributes[Key.SlowRemainingMs]; set => Attributes[Key.SlowRemainingMs] = value; }
    public int HasteRemainingMs { get => Attributes[Key.HasteRemainingMs]; set => Attributes[Key.HasteRemainingMs] = value; }
    public bool InFlight { get => Attributes[Key.InFlight] != 0; set => Attributes[Key.InFlight] = value ? 1 : 0; }
    public bool Destroyed { get => Attributes[Key.Destroyed] != 0; set => Attributes[Key.Destroyed] = value ? 1 : 0; }
    public int CritTimeMs { get => Attributes[Key.CritTimeMs]; set => Attributes[Key.CritTimeMs] = value; }
    public bool IsCritThisUse { get => Attributes[Key.IsCritThisUse] != 0; set => Attributes[Key.IsCritThisUse] = value ? 1 : 0; }
    public int CritDamagePercentThisUse { get => Attributes[Key.CritDamage]; set => Attributes[Key.CritDamage] = value; }
    public int AmmoRemaining { get => Attributes[Key.Custom_2]; set => Attributes[Key.Custom_2] = value; }
    private readonly Dictionary<int, int> _lastTriggerMsByAbility = [];

    public ItemState(ItemTemplate template, ItemTier tier)
    {
        Template = template;
        for (int i = 0; i < Attributes.Length; i++)
            Attributes[i] = template.GetInt(i, tier, 0);
    }

    public int GetLastTriggerMs(int abilityIndex) =>
        _lastTriggerMsByAbility.TryGetValue(abilityIndex, out int v) ? v : int.MinValue;

    public void SetLastTriggerMs(int abilityIndex, int timeMs) =>
        _lastTriggerMsByAbility[abilityIndex] = timeMs;
}
