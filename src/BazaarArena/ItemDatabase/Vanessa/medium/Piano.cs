using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>钢琴（Piano）：海盗中型；使用伙伴时为其加速；相邻视为伙伴。</summary>
public static class Piano
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "钢琴",
            Desc = "使用伙伴时，使其加速 {HasteSeconds} 秒；相邻物品变为伙伴",
            Tags = 0,
            Cooldown = 0.0,
            Haste = [1.0, 2.0],
            HasteTargetCount = 1,
            Abilities =
            [
                Ability.Haste.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.WithTag(Tag.Friend),
                    targetCondition: Condition.SameAsInvokeTarget,
                    priority: AbilityPriority.Medium),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Tags,
                    Condition = Condition.AdjacentToCaster,
                    Value = Formula.Constant(Tag.Friend),
                },
            ],
        };
    }
}
