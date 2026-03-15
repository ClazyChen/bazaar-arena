using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>龙涎香（Ambergris）：海盗小型水系、遗物；治疗 = (价值 + Custom_1×Custom_2) × Custom_0（光环）；购买水系物品时价值提高 Custom_1。</summary>
public static class Ambergris
{
    /// <summary>龙涎香（版本 5）：4s 小 铜 水系 遗物；▶ 获得治疗，等量于此物品价值的 1 » 2 » 3 » 4 倍（公式 (Price + Custom_1×Custom_2) × Custom_0）；购买水系物品时，此物品的价值提高 1 » 2 » 3 » 4。Custom_2 可覆盖，默认 5 » 10 » 15 » 20。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "龙涎香",
            Desc = "▶ 获得治疗，等量于此物品价值的 {Custom_0} 倍；购买水系物品时，此物品的价值提高 {Custom_1}",
            Tags = [Tag.Aquatic, Tag.Relic],
            Cooldown = 4.0,
            Custom_0 = [1, 2, 3, 4],
            Custom_1 = [1, 2, 3, 4],
            OverridableAttributes = new Dictionary<string, IntOrByTier>
            {
                [Key.Custom_2] = [5, 10, 15, 20],
            },
            Abilities =
            [
                Ability.Heal
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.Heal,
                    Value = (Formula.Source(Key.Price) + Formula.Source(Key.Custom_1) * Formula.Source(Key.Custom_2)) * Formula.Source(Key.Custom_0),
                },
            ],
        };
    }
}
