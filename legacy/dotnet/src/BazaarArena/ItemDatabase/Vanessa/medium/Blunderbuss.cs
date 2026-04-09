using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>雷筒（Blunderbuss）：海盗中型武器（钻档）；触发灼烧时使用此物品。</summary>
public static class Blunderbuss
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "雷筒",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；触发灼烧时，使用此物品",
            Tags = Tag.Weapon,
            Cooldown = 5.0,
            Damage = 30,
            AmmoCap = 5,
            Abilities =
            [
                Ability.Damage,
                Ability.UseThisItem.Override(
                    trigger: Trigger.Burn,
                    priority: AbilityPriority.Low),
            ],
        };
    }
}
