using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>水草（Seaweed）：海盗小型水系；▶ 治疗 20 生命值；使用水系物品时，此物品的治疗提高 5 » 10 » 15 » 20（限本场战斗）。</summary>
public static class Seaweed
{
    /// <summary>水草：7s 小 铜 水系；▶ 治疗 {Heal} 生命值；使用水系物品时，此物品的治疗提高 {Custom_0}（限本场战斗）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "水草",
            Desc = "▶ 治疗 {Heal} 生命值；使用水系物品时，此物品的治疗提高 {Custom_0}（限本场战斗）",
            Tags = [Tag.Aquatic],
            Cooldown = 7.0,
            Heal = 20,
            Custom_0 = [5, 10, 15, 20],
            Abilities =
            [
                Ability.Heal,
                Ability.AddAttribute(Key.Heal).Override(
                    trigger: Trigger.UseItem,
                    condition: Condition.SameSide & Condition.WithTag(Tag.Aquatic),
                    targetCondition: Condition.SameAsSource
                ),
            ],
        };
    }
}
