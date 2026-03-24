using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace BazaarArena.Core;

[CollectionBuilder(typeof(SecondsOrByTier), "Create")]
public readonly struct SecondsOrByTier : IEnumerable<double>
{
    private readonly double[]? _values;
    private SecondsOrByTier(double[] values) => _values = values;
    public static SecondsOrByTier Create(ReadOnlySpan<double> values)
    {
        var arr = new double[values.Length];
        for (int i = 0; i < values.Length; i++) arr[i] = values[i];
        return new SecondsOrByTier(arr);
    }
    internal static SecondsOrByTier FromMilliseconds(IReadOnlyList<int>? ms)
    {
        if (ms == null || ms.Count == 0) return default;
        var arr = new double[ms.Count];
        for (int i = 0; i < ms.Count; i++) arr[i] = ms[i] / 1000.0;
        return new SecondsOrByTier(arr);
    }
    public static implicit operator SecondsOrByTier(double single) => new([single]);
    public static implicit operator SecondsOrByTier(double[] byTier) => new(byTier);
    internal List<int> ToMilliseconds() => _values?.Select(s => (int)(s * 1000)).ToList() ?? [];
    public IEnumerator<double> GetEnumerator() => (_values ?? []).AsEnumerable().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[CollectionBuilder(typeof(IntOrByTier), "Create")]
public readonly struct IntOrByTier : IEnumerable<int>
{
    private readonly List<int> _values;
    private IntOrByTier(List<int> values) => _values = values;
    public static IntOrByTier Create(ReadOnlySpan<int> values)
    {
        var list = new List<int>(values.Length);
        for (int i = 0; i < values.Length; i++) list.Add(values[i]);
        return new IntOrByTier(list);
    }
    public static implicit operator IntOrByTier(int single) => new(new List<int> { single });
    public static implicit operator IntOrByTier(int[] byTier) => new([.. byTier]);
    public List<int> ToList() => _values ?? [];
    public IntOrByTier Add(int delta) => [.. ToList().Select(v => v + delta).ToList()];
    public IEnumerator<int> GetEnumerator() => (_values ?? []).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class ItemTemplate
{
    private static readonly Dictionary<string, int> KeyByName = typeof(Key)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(int))
        .ToDictionary(f => f.Name, f => (int)f.GetValue(null)!);

    private static bool TryResolveKey(string keyName, out int key) => KeyByName.TryGetValue(keyName, out key);

    public ItemTemplate()
    {
        Attributes[Key.Size] = ItemSize.Small;
        Attributes[Key.Hero] = Core.Hero.Common;
    }

    public string Name { get; set; } = "";
    public string Desc { get; set; } = "";
    public ItemTier MinTier { get; set; }
    public IntOrByTier[] Attributes { get; private set; } = new IntOrByTier[Key.ItemTemplateAttributeCount];

    public int GetInt(int key, ItemTier tier = ItemTier.Bronze)
    {
        if ((uint)key >= (uint)Attributes.Length) return 0;
        var list = Attributes[key].ToList();
        if (list.Count == 0) return 0;
        if (list.Count == 1) return list[0];
        if ((int)tier < (int)MinTier) return 0;
        var index = (int)tier - (int)MinTier;
        return index < list.Count ? list[index] : 0;
    }

    private void SetIntOrByTier(int key, IntOrByTier value)
    {
        if ((uint)key >= (uint)Attributes.Length) return;
        Attributes[key] = value;
    }
    private IntOrByTier GetIntOrByTier(int key)
    {
        if ((uint)key >= (uint)Attributes.Length) return default;
        return Attributes[key];
    }

    public void SetIntByKey(int key, int value) => SetIntOrByTier(key, value);

    public void SetIntOrByTierByKey(int key, IntOrByTier value) => SetIntOrByTier(key, value);

    public Dictionary<int, List<int>> GetIntsByTierSnapshot()
    {
        var snapshot = new Dictionary<int, List<int>>(Attributes.Length);
        for (int i = 0; i < Attributes.Length; i++)
            snapshot[i] = Attributes[i].ToList();
        return snapshot;
    }

    public void SetIntsByTier(Dictionary<int, List<int>> snapshot)
    {
        foreach (var kv in snapshot)
            SetIntOrByTier(kv.Key, [.. kv.Value]);
    }

    public Dictionary<string, List<int>> GetIntsByTierView()
    {
        var view = new Dictionary<string, List<int>>(KeyByName.Count);
        foreach (var kv in KeyByName)
            view[kv.Key] = Attributes[kv.Value].ToList();
        return view;
    }

    public int GetInt(string keyName, ItemTier tier = ItemTier.Bronze)
    {
        if (!TryResolveKey(keyName, out int key)) return 0;
        return GetInt(key, tier);
    }

    public void SetInt(string keyName, int value)
    {
        if (!TryResolveKey(keyName, out int key)) return;
        SetIntByKey(key, value);
    }

    public SecondsOrByTier Cooldown { get => SecondsOrByTier.FromMilliseconds(GetIntOrByTier(Key.CooldownMs).ToList()); set => SetIntOrByTier(Key.CooldownMs, [.. value.ToMilliseconds()]); }
    public IntOrByTier CritRate { get => GetIntOrByTier(Key.CritRate); set => SetIntOrByTier(Key.CritRate, value); }
    public IntOrByTier CritDamage { get => GetIntOrByTier(Key.CritDamage); set => SetIntOrByTier(Key.CritDamage, value); }
    public IntOrByTier Multicast { get => GetIntOrByTier(Key.Multicast); set => SetIntOrByTier(Key.Multicast, value); }
    public IntOrByTier AmmoCap { get => GetIntOrByTier(Key.AmmoCap); set => SetIntOrByTier(Key.AmmoCap, value); }
    public IntOrByTier Damage { get => GetIntOrByTier(Key.Damage); set => SetIntOrByTier(Key.Damage, value); }
    public IntOrByTier Burn { get => GetIntOrByTier(Key.Burn); set => SetIntOrByTier(Key.Burn, value); }
    public IntOrByTier Poison { get => GetIntOrByTier(Key.Poison); set => SetIntOrByTier(Key.Poison, value); }
    public IntOrByTier Heal { get => GetIntOrByTier(Key.Heal); set => SetIntOrByTier(Key.Heal, value); }
    public IntOrByTier Shield { get => GetIntOrByTier(Key.Shield); set => SetIntOrByTier(Key.Shield, value); }
    public SecondsOrByTier Charge { get => SecondsOrByTier.FromMilliseconds(GetIntOrByTier(Key.Charge).ToList()); set => SetIntOrByTier(Key.Charge, [.. value.ToMilliseconds()]); }
    public IntOrByTier ChargeTargetCount { get => GetIntOrByTier(Key.ChargeTargetCount); set => SetIntOrByTier(Key.ChargeTargetCount, value); }
    public SecondsOrByTier Freeze { get => SecondsOrByTier.FromMilliseconds(GetIntOrByTier(Key.Freeze).ToList()); set => SetIntOrByTier(Key.Freeze, [.. value.ToMilliseconds()]); }
    public IntOrByTier FreezeTargetCount { get => GetIntOrByTier(Key.FreezeTargetCount); set => SetIntOrByTier(Key.FreezeTargetCount, value); }
    public IntOrByTier PercentFreezeReduction { get => GetIntOrByTier(Key.PercentFreezeReduction); set => SetIntOrByTier(Key.PercentFreezeReduction, value); }
    public SecondsOrByTier Slow { get => SecondsOrByTier.FromMilliseconds(GetIntOrByTier(Key.Slow).ToList()); set => SetIntOrByTier(Key.Slow, [.. value.ToMilliseconds()]); }
    public IntOrByTier SlowTargetCount { get => GetIntOrByTier(Key.SlowTargetCount); set => SetIntOrByTier(Key.SlowTargetCount, value); }
    public SecondsOrByTier Haste { get => SecondsOrByTier.FromMilliseconds(GetIntOrByTier(Key.Haste).ToList()); set => SetIntOrByTier(Key.Haste, [.. value.ToMilliseconds()]); }
    public IntOrByTier HasteTargetCount { get => GetIntOrByTier(Key.HasteTargetCount); set => SetIntOrByTier(Key.HasteTargetCount, value); }
    public IntOrByTier ReloadTargetCount { get => GetIntOrByTier(Key.ReloadTargetCount); set => SetIntOrByTier(Key.ReloadTargetCount, value); }
    public IntOrByTier RepairTargetCount { get => GetIntOrByTier(Key.RepairTargetCount); set => SetIntOrByTier(Key.RepairTargetCount, value); }
    public IntOrByTier DestroyTargetCount { get => GetIntOrByTier(Key.DestroyTargetCount); set => SetIntOrByTier(Key.DestroyTargetCount, value); }
    public IntOrByTier ModifyAttributeTargetCount { get => GetIntOrByTier(Key.ModifyAttributeTargetCount); set => SetIntOrByTier(Key.ModifyAttributeTargetCount, value); }
    public IntOrByTier LifeSteal { get => GetIntOrByTier(Key.LifeSteal); set => SetIntOrByTier(Key.LifeSteal, value); }
    public IntOrByTier Custom_0 { get => GetIntOrByTier(Key.Custom_0); set => SetIntOrByTier(Key.Custom_0, value); }
    public IntOrByTier Custom_1 { get => GetIntOrByTier(Key.Custom_1); set => SetIntOrByTier(Key.Custom_1, value); }
    public IntOrByTier Custom_2 { get => GetIntOrByTier(Key.Custom_2); set => SetIntOrByTier(Key.Custom_2, value); }
    public IntOrByTier Custom_3 { get => GetIntOrByTier(Key.Custom_3); set => SetIntOrByTier(Key.Custom_3, value); }
    public IntOrByTier Value { get => GetIntOrByTier(Key.Value); set => SetIntOrByTier(Key.Value, value); }
    public IntOrByTier Tags { get => GetIntOrByTier(Key.Tags); set => SetIntOrByTier(Key.Tags, value); }
    public IntOrByTier DerivedTags { get => GetIntOrByTier(Key.DerivedTags); set => SetIntOrByTier(Key.DerivedTags, value); }
    public int Size { get => GetInt(Key.Size, MinTier); set => SetIntOrByTier(Key.Size, value); }
    public int Hero { get => GetInt(Key.Hero, MinTier); set => SetIntOrByTier(Key.Hero, value); }

    public Dictionary<int, IntOrByTier>? OverridableAttributes { get; set; }
    public List<AbilityDefinition> Abilities { get; set; } = [];
    public List<AuraDefinition> Auras { get; set; } = [];
}