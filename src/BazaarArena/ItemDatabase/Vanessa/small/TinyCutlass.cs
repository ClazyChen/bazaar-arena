using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>迷你弯刀（Tiny Cutlass）：海盗小型武器；版本 S5，铜档；造成 6 » 12 » 24 » 48 伤害，多重释放 2，双倍暴击伤害。</summary>
public static class TinyCutlass
{
    /// <summary>迷你弯刀（S5）：6s 小 铜 武器；▶ 造成 6 » 12 » 24 » 48 伤害；多重释放：2；此物品能造成双倍暴击伤害。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "迷你弯刀",
            Desc = "▶ 造成 {Damage} 伤害；多重释放：{Multicast}；此物品能造成双倍暴击伤害",
            Tags = [Tag.Weapon],
            Cooldown = 6.0,
            Damage = [6, 12, 24, 48],
            Multicast = 2,
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.CritDamagePercent,
                    Value = Formula.Constant(100),
                    Percent = true,
                },
            ],
        };
    }
}
