using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class FloorSpike
{
    private static Formula AnySideWeaponUsed { get; } =
        Condition.DifferentFromCaster & Condition.WithTag(Tag.Weapon);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "地刺陷阱",
            Desc = "▶造成 {Damage} 伤害；▶造成 {Poison} 剧毒；任意玩家使用武器时，为此物品充能 {ChargeSeconds} 秒",
            Cooldown = [9.0, 8.0, 7.0, 6.0],
            Tags = Tag.Weapon | Tag.Trap,
            Damage = 20,
            Poison = 2,
            Charge = 1.0,
            Abilities =
            [
                Ability.Damage,
                Ability.Poison,
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    condition: AnySideWeaponUsed,
                    targetCondition: Condition.SameAsCaster
                ).Also(trigger: Trigger.UseItem),
            ],
        };
    }
}

