using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>侦查望远镜（Spyglass）：海盗中型工具。</summary>
public static class Spyglass
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "侦查望远镜",
            Desc = "触发减速时，{ModifyAttributeTargetCount} 件物品暴击率提高 {+Custom_0%}（限本场战斗）；战斗开始时，使 {ModifyAttributeTargetCount} 件敌方物品的冷却时间延长 {ChargeSeconds} 秒（限本场战斗）",
            Tags = Tag.Tool,
            Cooldown = 0.0,
            Custom_0 = 10,
            Charge = [3.0, 6.0],
            ModifyAttributeTargetCount = 1,
            Abilities =
            [
                Ability.AddAttribute(Key.CritRate).Override(
                    trigger: Trigger.Slow,
                    additionalTargetCondition: Condition.CanCrit,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low),
                Ability.AddAttribute(Key.CooldownMs).Override(
                    trigger: Trigger.BattleStart,
                    targetCondition: Condition.DifferentSide & Condition.HasCooldown,
                    valueKey: Key.Charge,
                    priority: AbilityPriority.Low),
            ],
        };
    }

    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "侦查望远镜_S1",
            Desc = "相邻物品 {+Custom_0%} 暴击率；战斗开始时，使 {ModifyAttributeTargetCount} 件敌方物品的冷却时间延长 {ChargeSeconds} 秒（限本场战斗）",
            Tags = Tag.Tool,
            Cooldown = 0.0,
            Custom_0 = [25, 50],
            Charge = [3.0, 6.0],
            ModifyAttributeTargetCount = 1,
            Abilities =
            [
                Ability.AddAttribute(Key.CooldownMs).Override(
                    trigger: Trigger.BattleStart,
                    targetCondition: Condition.DifferentSide,
                    additionalTargetCondition: Condition.HasCooldown,
                    valueKey: Key.Charge,
                    priority: AbilityPriority.Low),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Condition = Condition.AdjacentToCaster,
                    Value = Formula.Caster(Key.Custom_0),
                },
            ],
        };
    }
}
