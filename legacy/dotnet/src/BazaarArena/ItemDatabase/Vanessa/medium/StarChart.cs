using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>星图（Star Chart）：海盗中型工具、遗物。相邻物品暴击率 +10 » 15 » 20 » 25%；相邻物品冷却时间缩短 5 » 10 » 15 » 20%。</summary>
public static class StarChart
{
    /// <summary>星图：5s 中 铜 工具 遗物；相邻物品暴击率 +10 » 15 » 20 » 25%；相邻物品冷却时间缩短 5 » 10 » 15 » 20%。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "星图",
            Desc = "相邻物品 {+Custom_0%} 暴击率；相邻物品冷却时间缩短 {Custom_1%}",
            Tags = Tag.Tool | Tag.Relic,
            Cooldown = 5.0,
            Custom_0 = [10, 15, 20, 25],
            Custom_1 = [5, 10, 15, 20],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Condition = Condition.AdjacentToCaster,
                    Value = Formula.Caster(Key.Custom_0),
                },
                new AuraDefinition
                {
                    Attribute = Key.PercentCooldownReduction,
                    Condition = Condition.AdjacentToCaster,
                    Value = Formula.Caster(Key.Custom_1),
                },
            ],
        };
    }
}
