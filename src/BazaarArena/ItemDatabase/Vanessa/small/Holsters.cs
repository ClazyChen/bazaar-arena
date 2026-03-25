using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>枪套（Holsters）及其历史版本：海盗小型服饰；在己方首次使用物品或战斗开始时加速己方小型物品。</summary>
public static class Holsters
{
    /// <summary>枪套（版本 12，银）：0s 小 银 服饰；每场战斗己方首次使用物品时，加速己方小型物品 1 » 2 » 3 秒（High）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "枪套",
            Desc = "每场战斗己方首次使用物品时，加速己方小型物品 {HasteSeconds} 秒",
            Tags = Tag.Apparel,
            Cooldown = 0.0,
            Haste = [1.0, 2.0, 3.0],
            Custom_0 = 0,
            Custom_1 = 1,
            Abilities =
            [
                Ability.Haste.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.CasterCustom0IsZero,
                    additionalTargetCondition: Condition.WithTag(Tag.Small),
                    priority: AbilityPriority.High
                ),
                Ability.AddAttribute(Key.Custom_0).Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.CasterCustom0IsZero,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_1,
                    effectLogName: "",
                    priority: AbilityPriority.Immediate
                ),
            ],
        };
    }

    /// <summary>枪套_S4（版本 4，金）：0s 小 金 服饰；战斗开始时，加速己方小型物品 1 » 2 秒（High）。</summary>
    public static ItemTemplate Template_S4()
    {
        return new ItemTemplate
        {
            Name = "枪套_S4",
            Desc = "战斗开始时，加速己方小型物品 {HasteSeconds} 秒",
            Tags = Tag.Apparel,
            Cooldown = 0.0,
            Haste = [1.0, 2.0],
            Abilities =
            [
                Ability.Haste.Override(
                    trigger: Trigger.BattleStart,
                    additionalTargetCondition: Condition.WithTag(Tag.Small),
                    priority: AbilityPriority.High
                ),
            ],
        };
    }
}

