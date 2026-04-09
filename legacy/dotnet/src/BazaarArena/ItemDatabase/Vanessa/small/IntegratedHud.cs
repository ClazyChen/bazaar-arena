using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>集成式HUD（Integrated HUD）：海盗小型服饰、科技；此物品右侧的物品暴击率提高；右侧物品造成暴击时减速。</summary>
public static class IntegratedHud
{
    /// <summary>集成式HUD（版本 11，银）：无冷却 小 银 服饰 科技；此物品右侧的物品 +20 » +30 » +40% 暴击率；使用此物品右侧的物品造成暴击时，减速 1 件物品 1 秒。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "集成式HUD",
            Desc = "此物品右侧的物品 {+Custom_0%} 暴击率；使用此物品右侧的物品造成暴击时，减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒",
            Tags = Tag.Apparel | Tag.Tech,
            Cooldown = 0.0,
            Custom_0 = [20, 30, 40],
            Slow = 1.0,
            SlowTargetCount = 1,
            Abilities =
            [
                Ability.Slow.Override(
                    trigger: Trigger.Crit,
                    additionalCondition: Condition.RightOfCaster,
                    priority: AbilityPriority.Medium
                ),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Condition = Condition.RightOfCaster & Condition.CanCrit,
                    Value = Formula.Caster(Key.Custom_0),
                },
            ],
        };
    }
}

