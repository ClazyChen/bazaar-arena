using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>刺刀（Bayonet）：海盗小型武器；使用此物品左侧的武器时，造成 10 » 15 » 20 » 25 伤害。</summary>
public static class Bayonet
{
    /// <summary>刺刀：0s 小 铜 武器；使用此物品左侧的武器时，造成 10 » 15 » 20 » 25 伤害。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "刺刀",
            Desc = "使用此物品左侧的武器时，造成 {Damage} 伤害",
            Tags = [Tag.Weapon],
            Cooldown = 0,
            Damage = [10, 15, 20, 25],
            Abilities =
            [
                Ability.Damage.Override(
                    trigger: Trigger.UseItem,
                    condition: Condition.SameSide & Condition.LeftOfCaster & Condition.WithTag(Tag.Weapon)
                ),
            ],
        };
    }
}
