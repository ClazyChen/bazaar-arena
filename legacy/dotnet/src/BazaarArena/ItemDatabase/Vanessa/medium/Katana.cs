using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>武士刀（Katana）：海盗中型武器。▶ 造成伤害。</summary>
public static class Katana
{
    /// <summary>武士刀：2s 中 铜 武器；▶ 造成 5 » 10 » 15 » 20 伤害。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "武士刀",
            Desc = "▶ 造成 {Damage} 伤害",
            Tags = Tag.Weapon,
            Cooldown = 2.0,
            Damage = [5, 10, 15, 20],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }
}
