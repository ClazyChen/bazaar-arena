using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class PhilosophersStone
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "贤者之石",
            Desc =
                "▶获得 {Regen} 生命再生；【局外】转化原料时，此物品的生命再生提高 {Custom_0}；【默认】已转化的原料数量：{Custom_1}；【局外】购买此物品时，获得 1 件催化剂",
            Cooldown = 5.0,
            Tags = Tag.Relic,
            Regen = 1,
            Custom_0 = [2, 3, 4, 5],
            Custom_1 = [2, 4, 6, 8],
            OverridableAttributes = new Dictionary<int, IntOrByTier>
            {
                // 【默认】已转化的原料数量：2/4/6/8
                [Key.Custom_1] = [2, 4, 6, 8],
            },
            Abilities =
            [
                Ability.Regen,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Regen,
                    Value = Formula.Caster(Key.Custom_0) * Formula.Caster(Key.Custom_1),
                },
            ],
        };
    }
}

