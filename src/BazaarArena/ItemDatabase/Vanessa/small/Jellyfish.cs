using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>水母（Jellyfish）：海盗小型水系、伙伴；▶ 造成 3 » 6 » 9 » 12 剧毒；使用相邻的水系物品时，加速此物品 1 » 2 » 3 » 4 秒。</summary>
public static class Jellyfish
{
    /// <summary>水母：7s 小 铜 水系 伙伴；▶ 造成 {Poison} 剧毒；使用相邻的水系物品时，加速此物品 {HasteSeconds} 秒。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "水母",
            Desc = "▶ 造成 {Poison} 剧毒；使用相邻的水系物品时，加速此物品 {HasteSeconds} 秒",
            Tags = [Tag.Aquatic, Tag.Friend],
            Cooldown = 7.0,
            Poison = [3, 6, 9, 12],
            Haste = [1.0, 2.0, 3.0, 4.0],
            Abilities =
            [
                Ability.Poison,
                Ability.Haste.Override(
                    trigger: Trigger.UseOtherItem,
                    condition: Condition.SameSide & Condition.WithTag(Tag.Aquatic) & Condition.AdjacentToCaster,
                    targetCondition: Condition.SameAsCaster
                ),
            ],
        };
    }
}
