using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>船锚（Anchor）：海盗中型武器、水系、工具。</summary>
public static class Anchor
{
    private static Formula DamageFromOppMaxHpPercent { get; } =
        RatioUtil.PercentFloor(Formula.Opp(Key.MaxHp), Formula.Caster(Key.Custom_0));

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "船锚",
            Desc = "▶ 造成伤害，等量于敌人最大生命值的 {Custom_0%}；使用相邻物品时，加速此物品 {HasteSeconds} 秒",
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Tool,
            Cooldown = [12.0, 10.0],
            Damage = 0,
            Custom_0 = [20, 30],
            Haste = 2.0,
            HasteTargetCount = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.Haste.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.AdjacentToCaster,
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
