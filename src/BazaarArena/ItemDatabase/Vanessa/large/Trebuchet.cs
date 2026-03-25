using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

public static class Trebuchet
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "抛石机",
            Desc = "▶ 造成 {Damage} 伤害；▶ 造成 {Burn} 灼烧；使用其他的武器或触发加速时，为此物品充能 {ChargeSeconds} 秒",
            Cooldown = 10.0,
            Tags = Tag.Weapon,
            Damage = [75, 150, 250],
            Burn = [5, 15, 25],
            Charge = 2.0,
            Abilities =
            [
                Ability.Damage,
                Ability.Burn,
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.WithTag(Tag.Weapon),
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Low
                ).Also(trigger: Trigger.Haste),
            ],
        };
    }
}

