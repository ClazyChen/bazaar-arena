using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>战斗中单件物品的运行时状态（Attribute[Key.xxx]）。</summary>
public class ItemState
{
    private const int CrittableDerivedTagMask =
        DerivedTag.Damage | DerivedTag.Burn | DerivedTag.Poison | DerivedTag.Heal | DerivedTag.Shield | DerivedTag.Regen;

    public ItemTemplate Template { get; init; }
    public AbilityDefinition[] Abilities { get; init; } = [];
    public int[] Attributes { get; init; } = new int[Key.ItemStateAttributeCount];

    public int GetAttribute(int key) => Attributes[key];
    public void SetAttribute(int key, int value) => Attributes[key] = value;

    public int SideIndex { get => Attributes[Key.SideIndex]; set => Attributes[Key.SideIndex] = value; }
    public int ItemIndex { get => Attributes[Key.ItemIndex]; set => Attributes[Key.ItemIndex] = value; }
    public ItemTier Tier { get => (ItemTier)Attributes[Key.Tier]; set => Attributes[Key.Tier] = (int)value; }
    public int ChargedTimeMs { get => Attributes[Key.ChargedTimeMs]; set => Attributes[Key.ChargedTimeMs] = value; }
    public int FreezeRemainingMs { get => Attributes[Key.FreezeRemainingMs]; set => Attributes[Key.FreezeRemainingMs] = value; }
    public int SlowRemainingMs { get => Attributes[Key.SlowRemainingMs]; set => Attributes[Key.SlowRemainingMs] = value; }
    public int HasteRemainingMs { get => Attributes[Key.HasteRemainingMs]; set => Attributes[Key.HasteRemainingMs] = value; }
    public bool InFlight { get => Attributes[Key.InFlight] != 0; set => Attributes[Key.InFlight] = value ? 1 : 0; }
    public bool Destroyed { get => Attributes[Key.Destroyed] != 0; set => Attributes[Key.Destroyed] = value ? 1 : 0; }
    public int CritTimeMs { get => Attributes[Key.CritTimeMs]; set => Attributes[Key.CritTimeMs] = value; }
    public bool IsCritThisUse { get => Attributes[Key.IsCritThisUse] != 0; set => Attributes[Key.IsCritThisUse] = value ? 1 : 0; }
    public int CritDamage { get => Attributes[Key.CritDamage]; set => Attributes[Key.CritDamage] = value; }
    public int AmmoRemaining { get => Attributes[Key.AmmoRemaining]; set => Attributes[Key.AmmoRemaining] = value; }
    public bool HasUseItemSelfCritAbility =>
        Abilities.Any(a =>
            a.Apply != null
            && a.ApplyCritMultiplier
            && a.UseSelf
            && a.TriggerEntries.Any(e => e.Trigger == Trigger.UseItem));
    public bool CanCrit => ((GetAttribute(Key.DerivedTags) & CrittableDerivedTagMask) != 0) && HasUseItemSelfCritAbility;

    public ItemState(ItemTemplate template, ItemTier tier)
    {
        Template = template;
        Abilities = [.. template.Abilities];
        for (int i = 0; i < Attributes.Length; i++)
            Attributes[i] = template.GetInt(i, tier);
    }

    public ItemState(ItemState source)
    {
        Template = source.Template;
        Abilities = source.Abilities;
        Attributes = new int[Key.ItemStateAttributeCount];
        Array.Copy(source.Attributes, Attributes, Attributes.Length);
    }
}
