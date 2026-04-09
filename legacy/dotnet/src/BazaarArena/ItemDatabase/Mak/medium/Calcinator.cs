using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class Calcinator
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "煅烧釜",
            Desc =
                "▶造成 {Burn} 灼烧；【局外】转化原料时，此物品的灼烧提高 {Custom_0}；【默认】已转化的原料数量：{Custom_1}；【局外】每天开始时，花费 2 金币购买 1 件铅块",
            Cooldown = 7.0,
            Tags = Tag.Tool,
            Burn = 6,
            Custom_0 = [3, 5, 7, 9],
            Custom_1 = [2, 4, 6, 8],
            OverridableAttributes = new Dictionary<int, IntOrByTier>
            {
                // 【默认】已转化的原料数量：2/4/6/8
                [Key.Custom_1] = [2, 4, 6, 8],
            },
            Abilities =
            [
                Ability.Burn,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Burn,
                    Value = Formula.Caster(Key.Custom_0) * Formula.Caster(Key.Custom_1),
                },
            ],
        };
    }
}

