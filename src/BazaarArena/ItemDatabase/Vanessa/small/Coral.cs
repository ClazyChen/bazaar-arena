using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>珊瑚（Coral）及其历史版本：海盗小型遗物、水系；治疗 = 基础 20 + Custom_0×Custom_1（光环），Custom_0 为每次购买提高量，Custom_1 可覆盖表示已购买水系数量。</summary>
public static class Coral
{
    /// <summary>珊瑚（最新，对应表格版本 12）：5s 小 铜 水系 遗物；▶ 治疗 {Heal} 生命值（基础 20 + 光环 Custom_0×Custom_1）；购买水系物品时治疗量提高 {Custom_0}，Custom_1 可覆盖（默认 5/10/15/20）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "珊瑚",
            Desc = "▶ 治疗 {Heal} 生命值；购买水系物品时，此物品的治疗量提高 {Custom_0}（已购买 {Custom_1} 件水系物品）",
            Tags = [Tag.Aquatic, Tag.Relic],
            Cooldown = 5.0,
            Heal = 20,
            Custom_0 = [5, 10, 15, 20],
            OverridableAttributes = new Dictionary<string, IntOrByTier> {
                 [Key.Custom_1] = [5, 10, 15, 20] 
            },
            Abilities =
            [
                Ability.Heal,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.Heal,
                    Value = Formula.Source(Key.Custom_0) * Formula.Source(Key.Custom_1),
                },
            ],
        };
    }
}
