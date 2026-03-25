using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>定制准镜（Custom Scope）及其历史版本：海盗小型科技。</summary>
public static class CustomScope
{
    /// <summary>定制准镜（版本 9，银）：无冷却 小 银 科技；此物品右侧的武器 +20 » 30 » 40% 暴击率；如果只有 1 件武器，则其造成暴击时，为 1 件非武器物品充能 1 秒（Low）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "定制准镜",
            Desc = "此物品右侧的武器 {+Custom_0%} 暴击率；如果只有 1 件武器，则其造成暴击时，为 1 件非武器物品充能 {ChargeSeconds} 秒",
            Tags = Tag.Tech,
            Cooldown = 0.0,
            Custom_0 = [20, 30, 40],
            Charge = 1.0,
            Abilities =
            [
                Ability.Charge.Override(
                    trigger: Trigger.Crit,
                    additionalCondition: Condition.OnlyWeaponWithCooldown & Condition.RightOfCaster,
                    additionalTargetCondition: ~Condition.WithTag(Tag.Weapon),
                    priority: AbilityPriority.Low
                ),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Condition = Condition.RightOfCaster & Condition.WithTag(Tag.Weapon) & Condition.CanCrit,
                    Value = Formula.Caster(Key.Custom_0),
                },
            ],
        };
    }

    /// <summary>定制准镜_S2（版本 2，银）：无冷却 小 银 科技；此物品右侧的武器 +15 » 20 » 25% 暴击率；如果只有 1 件武器，则其造成暴击时，为 1 件非武器物品充能 1 » 1.5 » 2 秒（Low）。</summary>
    public static ItemTemplate Template_S2()
    {
        return new ItemTemplate
        {
            Name = "定制准镜_S2",
            Desc = "此物品右侧的武器 {+Custom_0%} 暴击率；如果只有 1 件武器，则其造成暴击时，为 1 件非武器物品充能 {ChargeSeconds} 秒",
            Tags = Tag.Tech,
            Cooldown = 0.0,
            Custom_0 = [15, 20, 25],
            Charge = [1.0, 1.5, 2.0],
            Abilities =
            [
                Ability.Charge.Override(
                    trigger: Trigger.Crit,
                    additionalCondition: Condition.OnlyWeaponWithCooldown & Condition.RightOfCaster,
                    additionalTargetCondition: ~Condition.WithTag(Tag.Weapon),
                    priority: AbilityPriority.Low
                ),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Condition = Condition.RightOfCaster & Condition.WithTag(Tag.Weapon) & Condition.CanCrit,
                    Value = Formula.Caster(Key.Custom_0),
                },
            ],
        };
    }
}

