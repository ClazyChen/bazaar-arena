using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>火药桶（Powder Keg）：海盗中型武器；伤害为敌方最大生命值比例。</summary>
public static class PowderKeg
{
    private static Formula DamageFromOppMaxHpPercent { get; } =
        RatioUtil.PercentFloor(Formula.Opp(Key.MaxHp), Formula.Caster(Key.Custom_0));

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "火药桶",
            Desc = "▶ 造成伤害，等量于敌人最大生命值的 {Custom_0%}；▶ 摧毁该物品；触发灼烧时，为此物品充能 {ChargeSeconds} 秒",
            Tags = Tag.Weapon,
            Cooldown = 24.0,
            Damage = 0,
            Custom_0 = [30, 40],
            Charge = 2.0,
            Abilities =
            [
                Ability.Damage.Override(
                    priority: AbilityPriority.Highest),
                Ability.Destroy.Override(
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Low),
                Ability.Charge.Override(
                    trigger: Trigger.Burn,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Medium),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Damage,
                    Condition = Condition.SameAsCaster,
                    Value = DamageFromOppMaxHpPercent,
                },
            ],
        };
    }
}
