using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>投掷飞刀（Throwing Knives）：海盗小型武器；▶ 造成伤害；弹药；其他物品暴击时使用此物品。</summary>
public static class ThrowingKnives
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "投掷飞刀",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；其他物品造成暴击时，使用此物品",
            Tags = Tag.Weapon,
            Cooldown = 4.0,
            Damage = 33,
            AmmoCap = [2, 4],
            Abilities =
            [
                Ability.Damage,
                Ability.UseThisItem.Override(
                    trigger: Trigger.Crit,
                    additionalCondition: Condition.DifferentFromCaster,
                    priority: AbilityPriority.Low),
            ],
        };
    }
}
