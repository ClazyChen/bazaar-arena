namespace BazaarArena.Core;

/// <summary>
/// 自动推导标签（与 Tag 分离，位从 1&lt;&lt;0 独立开始编号）。
/// 这类标签写入 Key.DerivedTags，不写入 Key.Tags。
/// </summary>
public static class DerivedTag
{
    public const int Shield = 1 << 0;
    public const int Damage = 1 << 1;
    public const int Ammo = 1 << 2;
    public const int Burn = 1 << 3;
    public const int Poison = 1 << 4;
    public const int Heal = 1 << 5;
    public const int Regen = 1 << 6;
    public const int Crit = 1 << 7;
    public const int Cooldown = 1 << 8;
    public const int Charge = 1 << 9;
    public const int Freeze = 1 << 10;
    public const int Slow = 1 << 11;
    public const int Haste = 1 << 12;
    public const int Reload = 1 << 13;
}
