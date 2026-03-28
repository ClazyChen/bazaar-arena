using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>逞威风腰带扣（Swash Buckle）：海盗中型服饰；相邻物品按暴击率获得伤害、护盾、治疗加成。</summary>
public static class SwashBuckle
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "逞威风腰带扣",
            Desc = "相邻物品 {+Custom_0%} 暴击率；相邻物品 +伤害，等量于其暴击率；相邻物品 +护盾，等量于其暴击率；相邻物品 +治疗，等量于其暴击率",
            Tags = Tag.Apparel,
            Cooldown = 0.0,
            Custom_0 = [15, 30],
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
                    Attribute = Key.Damage,
                    Condition = Condition.AdjacentToCaster,
                    Value = Formula.Item(Key.CritRate),
                },
                new AuraDefinition
                {
                    Attribute = Key.Shield,
                    Condition = Condition.AdjacentToCaster,
                    Value = Formula.Item(Key.CritRate),
                },
                new AuraDefinition
                {
                    Attribute = Key.Heal,
                    Condition = Condition.AdjacentToCaster,
                    Value = Formula.Item(Key.CritRate),
                },
            ],
        };
    }
}
