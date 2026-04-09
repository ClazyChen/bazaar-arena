using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>弯刀（Cutlass）：海盗中型武器。▶ 造成伤害；多重释放 2；此物品能造成双倍暴击伤害。</summary>
public static class Cutlass
{
    /// <summary>弯刀：5s 中 铜 武器；▶ 造成 10 » 20 » 30 » 40 伤害；多重释放：2；此物品能造成双倍暴击伤害。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "弯刀",
            Desc = "▶ 造成 {Damage} 伤害；多重释放：{Multicast}；此物品能造成双倍暴击伤害",
            Tags = Tag.Weapon,
            Cooldown = 5.0,
            Damage = [10, 20, 30, 40],
            Multicast = 2,
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritDamage,
                    Value = Formula.Constant(100),
                    Percent = true,
                },
            ],
        };
    }
}
