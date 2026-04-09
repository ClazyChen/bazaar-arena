using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>炮弹（Cannonball）：海盗小型；相邻物品 +2 » +3 » +4 最大弹药量。</summary>
public static class Cannonball
{
    /// <summary>炮弹（版本 12，银）：0s 小 银；相邻物品 +{Custom_0} 最大弹药量。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "炮弹",
            Desc = "相邻物品 +{Custom_0} 最大弹药量",
            Tags = 0,
            Cooldown = 0.0,
            Custom_0 = [2, 3, 4],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.AmmoCap,
                    Condition = Condition.AdjacentToCaster,
                    Value = Formula.Caster(Key.Custom_0),
                },
            ],
        };
    }
}

