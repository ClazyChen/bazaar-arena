using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Ramrod
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "填弹杆",
            Desc = "▶ 为此物品相邻的物品装填 {Reload} 发弹药；触发装填时，令其暴击率提高 {Custom_0%}（限本场战斗）",
            Cooldown = 3.0,
            Tags = Tag.Tool,
            Reload = [1, 2, 3],
            ReloadTargetCount = 2,
            Custom_0 = [10, 20, 30],
            Abilities =
            [
                Ability.Reload.Override(
                    additionalTargetCondition: Condition.AdjacentToCaster,
                    priority: AbilityPriority.Low),
                Ability.AddAttribute(Key.CritRate).Override(
                    trigger: Trigger.Reload,
                    additionalTargetCondition: Condition.SameAsInvokeTarget,
                    valueKey: Key.Custom_0),
            ],
        };
    }

    public static ItemTemplate Template_S5()
    {
        return new ItemTemplate
        {
            Name = "填弹杆_S5",
            Desc = "▶ 为此物品相邻的物品装填 {Reload} 发弹药；己方物品弹药耗尽时，为此物品充能 {ChargeSeconds} 秒",
            Cooldown = 5.0,
            Tags = Tag.Tool,
            Reload = [1, 2, 3],
            ReloadTargetCount = 2,
            Charge = 1.0,
            Abilities =
            [
                Ability.Reload.Override(
                    additionalTargetCondition: Condition.AdjacentToCaster,
                    priority: AbilityPriority.Low),
                Ability.Charge.Override(
                    trigger: Trigger.Ammo,
                    additionalCondition: Condition.AmmoDepleted,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Lowest),
            ],
        };
    }

    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "填弹杆_S1",
            Desc = "▶ 为此物品相邻的物品装填 {Reload} 发弹药；己方物品弹药耗尽时，为此物品充能 {ChargeSeconds} 秒；己方弹药物品 {+Custom_0%} 暴击率",
            Cooldown = 5.0,
            Tags = Tag.Tool,
            Reload = 1,
            ReloadTargetCount = 2,
            Charge = 1.0,
            Custom_0 = [20, 30, 40],
            Abilities =
            [
                Ability.Reload.Override(
                    additionalTargetCondition: Condition.AdjacentToCaster,
                    priority: AbilityPriority.Low),
                Ability.Charge.Override(
                    trigger: Trigger.Ammo,
                    additionalCondition: Condition.AmmoDepleted,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Lowest),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Condition = Condition.SameSide & Condition.WithDerivedTag(DerivedTag.Ammo) & Condition.CanCrit,
                    Value = Formula.Caster(Key.Custom_0),
                }
            ]
        };
    }
}

