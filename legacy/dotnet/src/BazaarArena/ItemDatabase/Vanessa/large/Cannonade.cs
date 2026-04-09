using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

/// <summary>火炮阵列（Cannonade）：海盗大型武器。</summary>
public static class Cannonade
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "火炮阵列",
            Desc = "▶ 造成 {Damage} 伤害；多重释放：{Multicast}；使用其他武器时，为此物品充能 {ChargeSeconds} 秒",
            Tags = Tag.Weapon,
            Cooldown = 14.0,
            Damage = [150, 200],
            Multicast = 3,
            Charge = 2.0,
            Abilities =
            [
                Ability.Damage.Override(priority: AbilityPriority.Low),
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.WithTag(Tag.Weapon),
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Medium),
            ],
        };
    }
}
