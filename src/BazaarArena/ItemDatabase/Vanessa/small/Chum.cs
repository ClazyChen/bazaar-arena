using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>鱼饵（Chum）：海盗小型水系、食物；▶ 使用此物品时水系物品暴击率提高 +3 » +6 » +9 » +12%（限本场战斗）。购买此物品时获得 1 食人鱼为局外成长，模拟器不实现。</summary>
public static class Chum
{
    /// <summary>鱼饵（版本 1）：4s 小 铜 水系 食物；▶ 水系物品暴击率提高 +{Custom_0}%（限本场战斗）（High）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "鱼饵",
            Desc = "▶ 水系物品暴击率提高 +{Custom_0}%（限本场战斗）",
            Tags = [Tag.Aquatic, Tag.Food],
            Cooldown = 4.0,
            Custom_0 = [3, 6, 9, 12],
            Abilities =
            [
                Ability.AddAttribute(Key.CritRate).Override(
                    additionalTargetCondition: Condition.WithTag(Tag.Aquatic),
                    priority: AbilityPriority.High
                ),
            ],
        };
    }
}
