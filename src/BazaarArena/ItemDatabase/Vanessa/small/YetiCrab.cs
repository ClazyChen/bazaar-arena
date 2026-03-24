using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>雪怪蟹（Yeti Crab）：海盗小型水系、伙伴；▶ 冻结 1 件物品 1 秒；触发冻结时，相邻剧毒物品的剧毒提高 2 » 4 » 6 » 8（限本场战斗）。</summary>
public static class YetiCrab
{
    /// <summary>雪怪蟹（最新版）：7s 小 铜 水系 伙伴；▶ 冻结 {FreezeTargetCount} 件物品 {FreezeSeconds} 秒；触发冻结时，相邻剧毒物品的剧毒提高 {Custom_0}（限本场战斗）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "雪怪蟹",
            Desc = "▶ 冻结 {FreezeTargetCount} 件物品 {FreezeSeconds} 秒；触发冻结时，相邻剧毒物品的剧毒提高 {Custom_0}（限本场战斗）",
            Tags = [Tag.Aquatic, Tag.Friend],
            Cooldown = 7.0,
            Freeze = 1.0,
            Custom_0 = [2, 4, 6, 8],
            Abilities =
            [
                Ability.Freeze,
                Ability.AddAttribute(Key.Poison).Override(
                    trigger: Trigger.Freeze,
                    additionalTargetCondition: Condition.AdjacentToCaster & Condition.WithDerivedTag(DerivedTag.Poison)
                ),
            ],
        };
    }
}
