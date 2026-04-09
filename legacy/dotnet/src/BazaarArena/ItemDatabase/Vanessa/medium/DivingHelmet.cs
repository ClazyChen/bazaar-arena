using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>潜水头盔（Diving Helmet）：海盗中型水系、工具、服饰。</summary>
public static class DivingHelmet
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "潜水头盔",
            Desc = "▶ 获得 {Shield} 护盾；▶ 为相邻物品充能 {ChargeSeconds} 秒；使用其他水系物品时，为此物品充能 {ChargeSeconds} 秒；相邻物品变为水系",
            Tags = Tag.Aquatic | Tag.Tool | Tag.Apparel,
            Cooldown = 8.0,
            Shield = [50, 100],
            Charge = [1.0, 2.0],
            ChargeTargetCount = 2,
            Abilities =
            [
                Ability.Shield,
                Ability.Charge.Override(
                    additionalTargetCondition: Condition.AdjacentToCaster,
                    priority: AbilityPriority.High),
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.WithTag(Tag.Aquatic),
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Charge,
                    priority: AbilityPriority.Medium),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Tags,
                    Condition = Condition.AdjacentToCaster,
                    Value = Formula.Constant(Tag.Aquatic),
                },
            ],
        };
    }

    /// <summary>银档历史版：护盾较低；使用水系物品时（含自身使用）提高此物品护盾；相邻变为水系。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "潜水头盔_S1",
            Desc = "▶ 获得 {Shield} 护盾；使用水系物品时，此物品的护盾提高 {Custom_0}（限本场战斗）；相邻物品变为水系",
            Tags = Tag.Aquatic | Tag.Tool | Tag.Apparel,
            Cooldown = 6.0,
            Shield = 25,
            Custom_0 = [10, 20, 30],
            Abilities =
            [
                Ability.Shield,
                Ability.AddAttribute(Key.Shield).Override(
                    trigger: Trigger.UseItem,
                    additionalCondition: Condition.WithTag(Tag.Aquatic),
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Medium),
                Ability.AddAttribute(Key.Shield).Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.WithTag(Tag.Aquatic),
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Medium),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Tags,
                    Condition = Condition.AdjacentToCaster,
                    Value = Formula.Constant(Tag.Aquatic),
                },
            ],
        };
    }
}
