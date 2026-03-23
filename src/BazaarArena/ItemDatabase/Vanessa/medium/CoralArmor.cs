using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>珊瑚护甲（Coral Armor）：海盗中型水系、服饰、遗物；护盾 = 基础 50 + Custom_0×Custom_1（光环），Custom_0 为每次购买提高量，Custom_1 可覆盖表示已购买水系数量。局外成长不实现。</summary>
public static class CoralArmor
{
    /// <summary>珊瑚护甲：6s 中 铜 水系 服饰 遗物；▶ 获得 {Shield} 护盾（基础 50 + 光环 Custom_0×Custom_1）；购买水系物品时护盾提高 {Custom_0}，Custom_1 可覆盖（默认 5/10/15/20）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "珊瑚护甲",
            Desc = "▶ 获得 {Shield} 护盾；购买水系物品时，此物品的护盾提高 {Custom_0}（已购买 {Custom_1} 件水系物品）",
            Tags = [Tag.Aquatic, Tag.Apparel, Tag.Relic],
            Cooldown = 6.0,
            Shield = 50,
            Custom_0 = [10, 20, 30, 40],
            OverridableAttributes = new Dictionary<int, IntOrByTier>
            {
                [Key.Custom_1] = [5, 10, 15, 20],
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
                    Value = Formula.Source(Key.Custom_0) * Formula.Source(Key.Custom_1),
                },
            ],
        };
    }
}
