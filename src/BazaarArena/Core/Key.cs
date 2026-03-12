namespace BazaarArena.Core;

/// <summary>物品模板字段名常量，与 ItemTemplate 属性一致，供能力/物品定义与 GetInt(key)、GetResolvedValue(key) 使用。</summary>
public static class Key
{
    public const string Damage = nameof(ItemTemplate.Damage);
    public const string Shield = nameof(ItemTemplate.Shield);
    public const string Heal = nameof(ItemTemplate.Heal);
    public const string Burn = nameof(ItemTemplate.Burn);
    public const string Poison = nameof(ItemTemplate.Poison);
    public const string Custom_0 = nameof(ItemTemplate.Custom_0);
}
