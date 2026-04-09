using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>套娃（Nesting Doll）：海盗小型玩具；▶ 获得护盾，等量于此物品剩余弹药量；弹药：8。每天开始提高最大弹药为局外成长，模拟器不实现触发；成长量为 Custom_0，成长次数为可覆盖 Custom_1。</summary>
public static class NestingDoll
{
    /// <summary>套娃（版本 1，银）：2s 小 银 玩具；▶ 获得护盾，等量于此物品剩余弹药量；弹药：{AmmoCap}。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "套娃",
            Desc = "▶ 获得护盾，等量于此物品剩余弹药量；弹药：{AmmoCap}；每天开始时，此物品的最大弹药量提高 {Custom_0}",
            Tags = Tag.Toy,
            Cooldown = 2.0,
            AmmoCap = 8,
            Custom_0 = [1, 2, 3],
            Custom_1 = [0, 0, 0],
            Shield = 0,
            OverridableAttributes = new Dictionary<int, IntOrByTier>
            {
                [Key.Custom_1] = [0, 2, 4],
            },
            Abilities =
            [
                Ability.Shield,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Shield,
                    Value = Formula.Caster(Key.AmmoRemaining),
                },
                new AuraDefinition
                {
                    Attribute = Key.AmmoCap,
                    Value = Formula.Caster(Key.Custom_0) * Formula.Caster(Key.Custom_1),
                },
            ],
        };
    }
}

