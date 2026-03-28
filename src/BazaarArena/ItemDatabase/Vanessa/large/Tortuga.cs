using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

/// <summary>巨龟托图加（Tortuga）：海盗大型武器、水系、载具、伙伴。</summary>
public static class Tortuga
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "巨龟托图加",
            Desc = "▶ 造成 {Damage} 伤害；▶ 加速其他物品 {HasteSeconds} 秒；使用其他伙伴时，为此物品充能 {ChargeSeconds} 秒",
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Vehicle | Tag.Friend,
            Cooldown = [12.0, 10.0],
            Damage = [450, 900],
            Haste = 1.0,
            Charge = 2.0,
            Abilities =
            [
                Ability.Damage,
                Ability.Haste.Override(
                    additionalTargetCondition: Condition.DifferentFromCaster,
                    priority: AbilityPriority.High),
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.WithTag(Tag.Friend),
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Medium),
            ],
        };
    }
}
