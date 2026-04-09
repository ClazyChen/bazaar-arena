using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

/// <summary>电鳗（Electric Eels）：海盗大型武器、水系、伙伴。</summary>
public static class ElectricEels
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "电鳗",
            Desc = "▶ 造成 {Damage} 伤害；▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；敌方使用物品时，为此物品充能 {ChargeSeconds} 秒",
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Friend,
            Cooldown = [7.0, 6.0],
            Damage = 100,
            Slow = 1.0,
            SlowTargetCount = 1,
            Charge = 2.0,
            Abilities =
            [
                Ability.Damage.Override(priority: AbilityPriority.Low),
                Ability.Slow.Override(priority: AbilityPriority.Low),
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    condition: Condition.DifferentSide,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Medium),
            ],
        };
    }
}
