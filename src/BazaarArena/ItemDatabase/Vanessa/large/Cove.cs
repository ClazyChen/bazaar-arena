using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

/// <summary>温馨海湾（Cove）：海盗大型水系、地产。▶ 获得护盾，等量于此物品价值的 1 » 2 » 3 » 4 倍（价值含出售提高，Custom_1 为已出售数量可覆盖）。出售物品时此物品价值提高 1 » 1 » 1 » 2（局外成长，不实现）。</summary>
public static class Cove
{
    /// <summary>温馨海湾：4s 大 铜 水系 地产；▶ 获得护盾，等量于此物品价值的 {Custom_0} 倍；出售物品时，此物品的价值提高 {Custom_2}（已出售 {Custom_1} 件）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "温馨海湾",
            Desc = "▶ 获得护盾，等量于此物品价值的 {Custom_0} 倍；出售物品时，此物品的价值提高 {Custom_2}（已出售 {Custom_1} 件）",
            Tags = [Tag.Aquatic, Tag.Property],
            Cooldown = 4.0,
            Custom_0 = [1, 2, 3, 4],
            Custom_2 = [1, 1, 1, 2],
            OverridableAttributes = new Dictionary<int, IntOrByTier>
            {
                [Key.Custom_1] = [10, 20, 40, 80],
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
                    Value = (Formula.Source(Key.Price) + Formula.Source(Key.Custom_1) * Formula.Source(Key.Custom_2)) * Formula.Source(Key.Custom_0),
                },
            ],
        };
    }
}
