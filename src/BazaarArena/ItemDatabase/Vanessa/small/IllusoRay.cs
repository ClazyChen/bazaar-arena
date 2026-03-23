using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>迷幻蝠鲼（IllusoRay）：海盗小型射线、水系、伙伴；减速 1 件物品，相邻伙伴或射线时获得多重释放。</summary>
public static class IllusoRay
{
    /// <summary>迷幻蝠鲼：6s 小 铜 水系 射线 伙伴；▶ 减速 1 件物品 1 » 2 » 3 » 4 秒；每有一个相邻的伙伴或射线，此物品 +1 多重释放。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "迷幻蝠鲼",
            Desc = "▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；每有一个相邻的伙伴或射线，此物品 +1 多重释放",
            Tags = [Tag.Aquatic, Tag.Ray, Tag.Friend],
            Cooldown = 6.0,
            Slow = [1.0, 2.0, 3.0, 4.0],
            Abilities =
            [
                Ability.Slow,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Value = Formula.Count(Condition.AdjacentToCaster & (Condition.WithTag(Tag.Friend) | Condition.WithTag(Tag.Ray))),
                },
            ],
        };
    }
}
