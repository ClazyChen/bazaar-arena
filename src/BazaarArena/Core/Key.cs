namespace BazaarArena.Core;

/// <summary>物品模板 Key 常量，用于将 Key 转换为下标，在更新的 ItemState 中使用。</summary>
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

    // 伤害
    public const int Damage = 0;
    // 灼烧
    public const int Burn = Damage + 1;
    // 剧毒
    public const int Poison = Burn + 1;
    // 护盾
    public const int Shield = Poison + 1;
    // 治疗
    public const int Heal = Shield + 1;
    // 生命再生
    public const int Regen = Heal + 1;
    // 暴击率
    public const int CritRate = Regen + 1;
    public const int CritRatePercent = CritRate;
    // 暴击伤害
    public const int CritDamage = CritRate + 1;
    public const int CritDamagePercent = CritDamage;
    // 多重释放
    public const int Multicast = CritDamage + 1;
    // 弹药上限
    public const int AmmoCap = Multicast + 1;
    // 充能
    public const int Charge = AmmoCap + 1;
    // 充能目标数量
    public const int ChargeTargetCount = Charge + 1;
    // 加速
    public const int Haste = ChargeTargetCount + 1;
    // 加速目标数量
    public const int HasteTargetCount = Haste + 1;
    // 装填
    public const int Reload = HasteTargetCount + 1;
    // 装填目标数量
    public const int ReloadTargetCount = Reload + 1;
    // 修复
    public const int Repair = ReloadTargetCount + 1;
    // 修复目标数量
    public const int RepairTargetCount = Repair + 1;
    // 冻结
    public const int Freeze = RepairTargetCount + 1;
    // 冻结目标数量
    public const int FreezeTargetCount = Freeze + 1;
    // 冻结时长减免百分比
    public const int PercentFreezeReduction = FreezeTargetCount + 1;
    // 减速
    public const int Slow = PercentFreezeReduction + 1;
    // 减速目标数量
    public const int SlowTargetCount = Slow + 1;
    // 减速时长减免百分比
    public const int PercentSlowReduction = SlowTargetCount + 1;
    // 摧毁目标数量
    public const int DestroyTargetCount = PercentSlowReduction + 1;
    // 冷却时间
    public const int CooldownMs = DestroyTargetCount + 1;
    // 冷却时间缩短百分比
    public const int PercentCooldownReduction = CooldownMs + 1;
    // 吸血
    public const int LifeSteal = PercentCooldownReduction + 1;
    // 修改属性目标数量
    public const int ModifyAttributeTargetCount = LifeSteal + 1;
    // 价值
    public const int Value = ModifyAttributeTargetCount + 1;
    // 标签
    public const int Tags = Value + 1;
    // 推导标签
    public const int DerivedTags = Tags + 1;
    // 尺寸
    public const int Size = DerivedTags + 1;
    // 英雄
    public const int Hero = Size + 1;
    public const int Custom_0 = Hero + 1;
    public const int Custom_1 = Custom_0 + 1;
    public const int Custom_2 = Custom_1 + 1;
    public const int Custom_3 = Custom_2 + 1;
    // 过渡期别名：复用自定义槽位，避免扩容 Attributes。
    public const int Price = Custom_0;
    public const int StashParameter = Custom_1;
    public const int AmmoRemaining = Custom_2;
    public const int ItemTemplateAttributeCount = Custom_3 + 1;

    // 运行时字段
    public const int InFlight = ItemTemplateAttributeCount;
    public const int Destroyed = ItemTemplateAttributeCount + 1;
    public const int ChargedTimeMs = InFlight + 1;
    public const int FreezeRemainingMs = ChargedTimeMs + 1;
    public const int SlowRemainingMs = FreezeRemainingMs + 1;
    public const int HasteRemainingMs = SlowRemainingMs + 1;
    public const int SideIndex = HasteRemainingMs + 1;
    public const int ItemIndex = SideIndex + 1;
    public const int Tier = ItemIndex + 1;
    public const int CritTimeMs = Tier + 1;
    public const int IsCritThisUse = CritTimeMs + 1;
    public const int ItemStateAttributeCount = IsCritThisUse + 1;

    // Side 状态
    public const int MaxHp = Damage;
    public const int Hp = Heal;
    public const int Gold = Regen + 1;
    public const int SideStateAttributeCount = Gold + 1;
}
