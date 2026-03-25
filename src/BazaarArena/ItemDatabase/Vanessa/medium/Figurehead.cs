using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Figurehead
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "船首像",
            Desc = "此物品左侧的所有水系物品冷却时间缩短 {Custom_0%}；此物品右侧的所有武器 {+Custom_1} 伤害",
            Cooldown = 0.0,
            Tags = Tag.Aquatic | Tag.Relic,
            Custom_0 = [10, 15, 20],
            Custom_1 = [25, 50, 100],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.PercentCooldownReduction,
                    Condition = Condition.StrictlyLeftOfCaster & Condition.WithTag(Tag.Aquatic),
                    Value = Formula.Caster(Key.Custom_0),
                },
                new AuraDefinition
                {
                    Attribute = Key.Damage,
                    Condition = Condition.StrictlyRightOfCaster & Condition.WithTag(Tag.Weapon),
                    Value = Formula.Caster(Key.Custom_1),
                },
            ],
        };
    }
}

