namespace BazaarArena.Core;

/// <summary>物品模板字段名常量，与 ItemTemplate 属性一致，供能力/物品定义与 GetInt(key)、GetResolvedValue(key) 使用。</summary>
public static class Key
{
    // 数值/类型属性
    public const string Damage = nameof(ItemTemplate.Damage);
    public const string Shield = nameof(ItemTemplate.Shield);
    public const string Heal = nameof(ItemTemplate.Heal);
    public const string Burn = nameof(ItemTemplate.Burn);
    public const string Poison = nameof(ItemTemplate.Poison);
    public const string Gold = nameof(ItemTemplate.Gold);
    public const string Custom_0 = nameof(ItemTemplate.Custom_0);
    public const string Custom_1 = nameof(ItemTemplate.Custom_1);
    public const string Custom_2 = nameof(ItemTemplate.Custom_2);
    /// <summary>物品价值（用于龙涎香等治疗公式）；注册时按尺寸自动设置默认值。</summary>
    public const string Price = nameof(ItemTemplate.Price);

    // 冷却与暴击
    public const string CooldownMs = nameof(ItemTemplate.CooldownMs);
    public const string CritRatePercent = nameof(ItemTemplate.CritRatePercent);
    public const string CritDamagePercent = nameof(ItemTemplate.CritDamagePercent);

    // 施放与弹药
    public const string Multicast = nameof(ItemTemplate.Multicast);
    public const string AmmoCap = nameof(ItemTemplate.AmmoCap);
    /// <summary>剩余弹药数，运行时存于物品模板字典（与 ItemTemplate.KeyAmmoRemaining 一致）。</summary>
    public const string AmmoRemaining = "AmmoRemaining";

    // 充能 / 冻结 / 减速 / 加速
    public const string Charge = nameof(ItemTemplate.Charge);
    public const string ChargeTargetCount = nameof(ItemTemplate.ChargeTargetCount);
    public const string Freeze = nameof(ItemTemplate.Freeze);
    public const string FreezeTargetCount = nameof(ItemTemplate.FreezeTargetCount);
    /// <summary>剩余冻结时间（毫秒），运行时存于物品模板字典。</summary>
    public const string FreezeRemainingMs = "FreezeRemainingMs";
    /// <summary>冻结时长减免百分比（0–100）；施加冻结时有效时长 = 原始时长 × (100 - 此值) / 100。默认 0。</summary>
    public const string PercentFreezeReduction = nameof(ItemTemplate.PercentFreezeReduction);
    /// <summary>冷却时间缩短百分比（0–100）；由光环提供，有效冷却 = 原冷却 × (100 - 此值) / 100，至少 1 秒。</summary>
    public const string PercentCooldownReduction = "PercentCooldownReduction";
    public const string Slow = nameof(ItemTemplate.Slow);
    public const string SlowTargetCount = nameof(ItemTemplate.SlowTargetCount);
    public const string Haste = nameof(ItemTemplate.Haste);
    public const string HasteTargetCount = nameof(ItemTemplate.HasteTargetCount);
    /// <summary>装填弹药目标数量（默认 1）；效果层 GetResolvedValue(ReloadTargetCount, defaultValue: 1)。</summary>
    public const string ReloadTargetCount = "ReloadTargetCount";

    // 修复 / 摧毁 / 吸血等
    public const string RepairTargetCount = nameof(ItemTemplate.RepairTargetCount);
    public const string DestroyTargetCount = nameof(ItemTemplate.DestroyTargetCount);
    public const string ModifyAttributeTargetCount = nameof(ItemTemplate.ModifyAttributeTargetCount);
    public const string LifeSteal = nameof(ItemTemplate.LifeSteal);

    // 其他
    public const string StashParameter = nameof(ItemTemplate.StashParameter);

    /// <summary>运行时飞行状态（与 ItemTemplate.KeyInFlight 一致）。</summary>
    public const string InFlight = "InFlight";
}
