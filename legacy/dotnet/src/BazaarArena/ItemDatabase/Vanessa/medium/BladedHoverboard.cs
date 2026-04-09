using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class BladedHoverboard
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "带刃悬浮板",
            Desc = "使用相邻物品时，造成 {Damage} 伤害",
            Cooldown = 0.0,
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Tech | Tag.Vehicle,
            Damage = [20, 40, 60],
            Abilities =
            [
                Ability.Damage.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.AdjacentToCaster),
            ],
        };
    }

    public static ItemTemplate Template_S4()
    {
        return new ItemTemplate
        {
            Name = "带刃悬浮板_S4",
            Desc = "使用相邻物品时，造成 {Damage} 伤害；相邻物品冷却时间缩短 {Custom_0%}",
            Cooldown = 0.0,
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Tech | Tag.Vehicle,
            Damage = [20, 30, 40],
            Custom_0 = [10, 15, 20],
            Abilities =
            [
                Ability.Damage.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.AdjacentToCaster),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.PercentCooldownReduction,
                    Condition = Condition.AdjacentToCaster,
                    Value = Formula.Caster(Key.Custom_0),
                },
            ],
        };
    }
}

