using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class BrokenBottle
{
    private static Formula OtherPotionUsed { get; } =
        Condition.SameSide & Condition.DifferentFromCaster & Condition.WithTag(Tag.Potion);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "碎瓶",
            Desc = "▶造成 {Damage} 伤害；弹药：{AmmoCap}；使用其他药水时，装填此物品；使用其他药水时，为此物品充能 {ChargeSeconds} 秒",
            Cooldown = 4.0,
            Tags = Tag.Weapon | Tag.Potion,
            Damage = [20, 40, 80, 160],
            AmmoCap = 1,
            Reload = 1,
            Charge = 1.0,
            Abilities =
            [
                Ability.Damage,
                Ability.Reload.Override(
                    trigger: Trigger.UseOtherItem,
                    condition: OtherPotionUsed,
                    targetCondition: Condition.SameAsCaster
                ),
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    condition: OtherPotionUsed,
                    targetCondition: Condition.SameAsCaster
                ),
            ],
        };
    }
}

