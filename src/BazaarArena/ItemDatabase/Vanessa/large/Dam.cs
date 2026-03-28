using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

/// <summary>大坝（Dam）：海盗大型水系、地产；摧毁双方中型与小型物品后自毁。</summary>
public static class Dam
{
    private static Formula IsMediumOrSmallTag { get; } =
        Condition.WithTag(Tag.Medium | Tag.Small);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "大坝",
            Desc = "▶ 摧毁双方所有中型和小型物品；▶ 摧毁此物品；使用其他水系物品时，为此物品充能 {ChargeSeconds} 秒",
            Tags = Tag.Aquatic | Tag.Property,
            Cooldown = 25.0,
            Charge = [1.0, 2.0],
            DestroyTargetCount = 20,
            Abilities =
            [
                Ability.Destroy.Override(
                    targetCondition: Condition.SameSide & IsMediumOrSmallTag,
                    priority: AbilityPriority.High),
                Ability.Destroy.Override(
                    targetCondition: Condition.DifferentSide & IsMediumOrSmallTag,
                    priority: AbilityPriority.High),
                Ability.Destroy.Override(
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Low),
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.WithTag(Tag.Aquatic),
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Medium),
            ],
        };
    }
}
