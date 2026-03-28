using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>刺刀手枪（Pistol Sword）：海盗中型武器。</summary>
public static class PistolSword
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "刺刀手枪",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；使用弹药物品时，造成 {Damage} 伤害",
            Tags = Tag.Weapon,
            Cooldown = 5.0,
            Damage = [15, 30],
            AmmoCap = 3,
            Abilities =
            [
                Ability.Damage,
                Ability.Damage.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.WithDerivedTag(DerivedTag.Ammo),
                    priority: AbilityPriority.Medium),
            ],
        };
    }
}
