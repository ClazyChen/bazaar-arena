using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Sextant
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "六分仪",
            Desc = "造成暴击时，加速 {HasteTargetCount} 件物品 {HasteSeconds} 秒；触发加速时，1 件物品暴击率提高 {Custom_0%}（限本场战斗）",
            Cooldown = 0.0,
            Tags = Tag.Aquatic | Tag.Tool,
            HasteTargetCount = 1,
            Haste = [1.0, 2.0, 3.0],
            ModifyAttributeTargetCount = 1,
            Custom_0 = 5,
            Abilities =
            [
                Ability.Haste.Override(
                    trigger: Trigger.Crit,
                    priority: AbilityPriority.Low),
                Ability.AddAttribute(Key.CritRate).Override(
                    trigger: Trigger.Haste,
                    additionalTargetCondition: Condition.CanCrit,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low),
            ],
        };
    }

    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "六分仪_S1",
            Desc = "造成暴击时，加速 {HasteTargetCount} 件物品 {HasteSeconds} 秒；相邻物品 {+Custom_0%} 暴击率",
            Cooldown = 0.0,
            Tags = Tag.Aquatic | Tag.Tool,
            Haste = [1.0, 2.0, 3.0],
            Custom_0 = [15, 30, 50],
            HasteTargetCount = 1,
            Abilities =
            [
                Ability.Haste.Override(
                    trigger: Trigger.Crit,
                    priority: AbilityPriority.Low),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Condition = Condition.AdjacentToCaster & Condition.CanCrit,
                    Value = Formula.Caster(Key.Custom_0),
                }
            ]
        };
    }
}

