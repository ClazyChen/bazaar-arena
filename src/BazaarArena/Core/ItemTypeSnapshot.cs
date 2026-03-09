namespace BazaarArena.Core;

/// <summary>物品导入时的类型快照：用于判断护盾/伤害/灼烧等类型与是否可暴击，避免战斗内数值被修改后误判。</summary>
public readonly struct ItemTypeSnapshot
{
    public bool IsDamageItem { get; }
    public bool IsBurnItem { get; }
    public bool IsPoisonItem { get; }
    public bool IsHealItem { get; }
    public bool IsShieldItem { get; }
    public bool IsRegenItem { get; }

    public ItemTypeSnapshot(bool isDamage, bool isBurn, bool isPoison, bool isHeal, bool isShield, bool isRegen)
    {
        IsDamageItem = isDamage;
        IsBurnItem = isBurn;
        IsPoisonItem = isPoison;
        IsHealItem = isHeal;
        IsShieldItem = isShield;
        IsRegenItem = isRegen;
    }

    /// <summary>是否具备可暴击的六类数值之一（Damage/Burn/Poison/Heal/Shield/Regen 任一 &gt; 0）。</summary>
    public bool HasAnyCrittableField =>
        IsDamageItem || IsBurnItem || IsPoisonItem || IsHealItem || IsShieldItem || IsRegenItem;

    /// <summary>根据模板与等级在导入时生成快照。</summary>
    public static ItemTypeSnapshot FromTemplate(ItemTemplate template, ItemTier tier)
    {
        return new ItemTypeSnapshot(
            template.GetInt(nameof(ItemTemplate.Damage), tier, 0) > 0,
            template.GetInt(nameof(ItemTemplate.Burn), tier, 0) > 0,
            template.GetInt(nameof(ItemTemplate.Poison), tier, 0) > 0,
            template.GetInt(nameof(ItemTemplate.Heal), tier, 0) > 0,
            template.GetInt(nameof(ItemTemplate.Shield), tier, 0) > 0,
            template.GetInt("Regen", tier, 0) > 0
        );
    }
}
