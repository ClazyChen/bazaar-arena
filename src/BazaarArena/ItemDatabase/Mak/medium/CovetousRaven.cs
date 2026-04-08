using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class CovetousRaven
{
    private static Formula OtherRelicUsed { get; } =
        Condition.SameSide & Condition.DifferentFromCaster & Condition.WithTag(Tag.Relic);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "贪婪渡鸦",
            Desc = "▶造成 {Damage} 伤害；使用其他遗物时，为此物品充能 {ChargeSeconds} 秒；使用其他遗物时，此物品开始飞行",
            Cooldown = 8.0,
            Tags = Tag.Weapon | Tag.Friend,
            Damage = [50, 100, 200, 400],
            Charge = 2.0,
            Custom_0 = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    condition: OtherRelicUsed,
                    targetCondition: Condition.SameAsCaster
                ),
                Ability.StartFlying.Override(
                    trigger: Trigger.UseOtherItem,
                    condition: OtherRelicUsed,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0
                ),
            ],
        };
    }
}

