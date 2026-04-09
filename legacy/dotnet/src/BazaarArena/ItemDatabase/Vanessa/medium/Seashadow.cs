using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Seashadow
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "海影宝驹",
            Desc = "▶ 其他物品冷却时间缩短 {Custom_0%}（限本场战斗）；▶ 此物品的冷却时间延长 {ChargeSeconds} 秒（限本场战斗）",
            Cooldown = 2.0,
            Tags = Tag.Friend | Tag.Vehicle,
            Custom_0 = 8,
            Charge = [4.0, 3.0, 2.0],
            Abilities =
            [
                Ability.AddAttribute(Key.PercentCooldownReduction).Override(
                    additionalCondition: Condition.DifferentFromCaster,
                    valueKey: Key.Custom_0),
                Ability.AddAttribute(Key.CooldownMs).Override(
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Charge,
                    priority: AbilityPriority.Low),
            ],
        };
    }
}

