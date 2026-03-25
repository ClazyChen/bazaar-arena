namespace BazaarArena.Core;

/// <summary>属性下标：物品模板、战斗内 ItemState 与 BattleSide 共用命名；阵营复用 Damage/Heal 等表示 MaxHp/Hp。</summary>
public static class Key
{
    private static readonly Dictionary<string, int> KeyByName = typeof(Key)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.FieldType == typeof(int))
        .ToDictionary(f => f.Name, f => (int)f.GetValue(null)!);
    private static readonly Dictionary<int, string> NameByKey = KeyByName
        .GroupBy(kv => kv.Value)
        .ToDictionary(g => g.Key, g => g.First().Key);

    public static bool TryGetKey(string name, out int key) => KeyByName.TryGetValue(name, out key);
    public static bool TryGetName(int key, out string name) => NameByKey.TryGetValue(key, out name!);
    public static string GetName(int key) => TryGetName(key, out var name) ? name : key.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>战斗内物品与阵营均为下标 0；物品模板该槽无数据（由 Run 写入）。</summary>
    public const int SideIndex = 0;
    /// <summary>物品伤害；阵营侧表示 <see cref="MaxHp"/>。</summary>
    public const int Damage = SideIndex + 1;
    public const int Burn = Damage + 1;
    public const int Poison = Burn + 1;
    public const int Shield = Poison + 1;
    /// <summary>物品治疗量字段；阵营侧表示当前 <see cref="Hp"/>。</summary>
    public const int Heal = Shield + 1;
    public const int Regen = Heal + 1;
    public const int CritRate = Regen + 1;
    public const int CritDamage = CritRate + 1;
    public const int Multicast = CritDamage + 1;
    public const int AmmoCap = Multicast + 1;
    public const int Charge = AmmoCap + 1;
    public const int ChargeTargetCount = Charge + 1;
    public const int Haste = ChargeTargetCount + 1;
    public const int HasteTargetCount = Haste + 1;
    public const int Reload = HasteTargetCount + 1;
    public const int ReloadTargetCount = Reload + 1;
    public const int Repair = ReloadTargetCount + 1;
    public const int RepairTargetCount = Repair + 1;
    public const int Freeze = RepairTargetCount + 1;
    public const int FreezeTargetCount = Freeze + 1;
    public const int PercentFreezeReduction = FreezeTargetCount + 1;
    public const int Slow = PercentFreezeReduction + 1;
    public const int SlowTargetCount = Slow + 1;
    public const int PercentSlowReduction = SlowTargetCount + 1;
    public const int DestroyTargetCount = PercentSlowReduction + 1;
    public const int CooldownMs = DestroyTargetCount + 1;
    public const int PercentCooldownReduction = CooldownMs + 1;
    public const int LifeSteal = PercentCooldownReduction + 1;
    public const int ModifyAttributeTargetCount = LifeSteal + 1;
    public const int Value = ModifyAttributeTargetCount + 1;
    public const int Tags = Value + 1;
    public const int DerivedTags = Tags + 1;
    /// <summary>物品是否可暴击（0/1），在 ItemDatabase.Register 阶段预计算。</summary>
    public const int CanCrit = DerivedTags + 1;
    public const int Size = CanCrit + 1;
    public const int Hero = Size + 1;
    /// <summary>获得无敌的持续时间（毫秒）。物品定义侧用 <see cref="ItemTemplate.Invincible"/> 以秒为单位写入。</summary>
    public const int InvincibleMs = Hero + 1;
    public const int Custom_0 = InvincibleMs + 1;
    public const int Custom_1 = Custom_0 + 1;
    public const int Custom_2 = Custom_1 + 1;
    public const int Custom_3 = Custom_2 + 1;
    public const int StashParameter = Custom_1;
    public const int AmmoRemaining = Custom_2;
    public const int ItemTemplateAttributeCount = Custom_3 + 1;

    public const int InFlight = ItemTemplateAttributeCount;
    public const int Destroyed = InFlight + 1;
    public const int ChargedTimeMs = Destroyed + 1;
    public const int FreezeRemainingMs = ChargedTimeMs + 1;
    public const int SlowRemainingMs = FreezeRemainingMs + 1;
    public const int HasteRemainingMs = SlowRemainingMs + 1;
    public const int ItemIndex = HasteRemainingMs + 1;
    public const int Tier = ItemIndex + 1;
    public const int CritTimeMs = Tier + 1;
    public const int IsCritThisUse = CritTimeMs + 1;
    public const int ItemStateAttributeCount = IsCritThisUse + 1;

    public const int MaxHp = Damage;
    public const int Hp = Heal;
    /// <summary>仅阵营使用；复用 <see cref="CritRate"/> 点位，避免物品属性额外占位。</summary>
    public const int Gold = CritRate;
    /// <summary>阵营无敌剩余时间（毫秒）。放在 SideStateAttributeCount 之前以便 BattleSide.Attributes 扩容。</summary>
    public const int InvincibleRemainingMs = CritDamage;

    /// <summary>BattleSide.Attributes 长度；下标 0～7 与 SideIndex～Gold（同 CritRate）一致。</summary>
    public const int SideStateAttributeCount = InvincibleRemainingMs + 1;
}
