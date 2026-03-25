using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>理查森先生（Mr. Richardson）：海盗小型水系、伙伴；▶ 获得护盾；触发加速/减速时，此物品护盾提高（限本场战斗）。</summary>
public static class MrRichardson
{
    /// <summary>理查森先生（版本 8，银）：6s 小 银 水系 伙伴；▶ 获得 30 护盾；触发加速/减速时，此物品护盾提高 5 » 10 » 15（High）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "理查森先生",
            Desc = "▶ 获得 {Shield} 护盾；触发加速时，此物品护盾提高 {Custom_0}（限本场战斗）；触发减速时，此物品护盾提高 {Custom_0}（限本场战斗）",
            Tags = Tag.Aquatic | Tag.Friend,
            Cooldown = 6.0,
            Shield = 30,
            Custom_0 = [5, 10, 15],
            Abilities =
            [
                Ability.Shield,
                Ability.AddAttribute(Key.Shield).Override(
                    trigger: Trigger.Haste,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.High
                ),
                Ability.AddAttribute(Key.Shield).Override(
                    trigger: Trigger.Slow,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.High
                ),
            ],
        };
    }
}

