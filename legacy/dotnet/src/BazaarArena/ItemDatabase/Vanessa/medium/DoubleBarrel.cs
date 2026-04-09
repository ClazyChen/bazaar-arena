using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>双管霰弹枪（Double Barrel）：海盗中型武器。▶ 造成伤害；弹药 2 发；多重释放 2。</summary>
public static class DoubleBarrel
{
    /// <summary>双管霰弹枪：4s 中 铜 武器；▶ 造成 20 » 40 » 60 » 80 伤害；弹药：2；多重释放：2。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "双管霰弹枪",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；多重释放：{Multicast}",
            Tags = Tag.Weapon,
            Cooldown = 4.0,
            Damage = [20, 40, 60, 80],
            AmmoCap = 2,
            Multicast = 2,
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }
}
