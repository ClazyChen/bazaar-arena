using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>龟壳（Turtle Shell）：海盗中型水系。</summary>
public static class TurtleShell
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "龟壳",
            Desc = "▶ 获得 {Shield} 护盾；▶ 护盾物品的护盾提高 {Custom_0}（限本场战斗）；使用其他非武器物品时，为此物品充能 {ChargeSeconds} 秒",
            Tags = Tag.Aquatic,
            Cooldown = 10.0,
            Shield = 15,
            Custom_0 = [15, 30],
            Charge = 2.0,
            Abilities =
            [
                Ability.Shield.Override(priority: AbilityPriority.Low),
                Ability.AddAttribute(Key.Shield).Override(
                    additionalTargetCondition: Condition.WithDerivedTag(DerivedTag.Shield),
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low),
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: ~Condition.WithTag(Tag.Weapon),
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Medium),
            ],
        };
    }
}
