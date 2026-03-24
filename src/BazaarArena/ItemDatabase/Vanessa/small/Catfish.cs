using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>毒须鲶（Poison Barb Catfish）：海盗小型水系、伙伴；▶ 造成 3 剧毒；此物品被加速时，其剧毒提高 2 » 4 » 6 » 8（限本场战斗）。</summary>
public static class Catfish
{
    /// <summary>毒须鲶（最新版）：5s 小 铜 水系 伙伴；▶ 造成 {Poison} 剧毒；此物品被加速时，其剧毒提高 {Custom_0}（限本场战斗）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "毒须鲶",
            Desc = "▶ 造成 {Poison} 剧毒；此物品被加速时，其剧毒提高 {Custom_0}（限本场战斗）",
            Tags = Tag.Aquatic | Tag.Friend,
            Cooldown = 5.0,
            Poison = 3,
            Custom_0 = [2, 4, 6, 8],
            Abilities =
            [
                Ability.Poison,
                Ability.AddAttribute(Key.Poison).Override(
                    trigger: Trigger.Haste,
                    condition: Condition.SameAsInvokeTarget,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }
}
