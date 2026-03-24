using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>狼筅（Langxian）：海盗中型武器、遗物。▶ 造成 40 伤害。伤害提高由光环 Custom_0×Custom_1 描述；使用此物品赢得战斗时 局外可提高 Custom_1，默认赢得战斗次数阈值 4/8/12/16 对应伤害提高 40/60/80/100。</summary>
public static class Langxian
{
    /// <summary>狼筅：10s 中 铜 武器 遗物；▶ 造成 40 伤害；此物品的伤害提高 = Custom_0×Custom_1（光环），Custom_0 为 40 » 60 » 80 » 100，Custom_1 可覆盖（默认 1，乘积即 40/60/80/100；局外赢得战斗次数阈值 4/8/12/16 时由局外更新）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "狼筅",
            Desc = "▶ 造成 {Damage} 伤害；使用此物品赢得战斗时，此物品的伤害提高 {Custom_0}",
            Tags = [Tag.Weapon, Tag.Relic],
            Cooldown = 10.0,
            Damage = 40,
            Custom_0 = [40, 60, 80, 100],
            OverridableAttributes = new Dictionary<int, IntOrByTier>
            {
                [Key.Custom_1] = [3, 6, 9, 12],
            },
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Damage,
                    Value = Formula.Caster(Key.Custom_0) * Formula.Caster(Key.Custom_1),
                },
            ],
        };
    }
}
