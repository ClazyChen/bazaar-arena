using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>冰镐（Ice Pick）：海盗小型武器、工具；▶ 造成 25 伤害；▶ 冻结 1 件物品 1 秒；触发冻结时，此物品伤害提高 15 » 30 » 45（限本场战斗）（Low）。</summary>
public static class IcePick
{
    /// <summary>冰镐（版本 12，银）：4s 小 银 武器 工具。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "冰镐",
            Desc = "▶ 造成 {Damage} 伤害；▶ 冻结 {FreezeTargetCount} 件物品 {FreezeSeconds} 秒；触发冻结时，此物品伤害提高 {Custom_0}（限本场战斗）",
            Tags = Tag.Weapon | Tag.Tool,
            Cooldown = 4.0,
            Damage = 25,
            Freeze = 1.0,
            Custom_0 = [15, 30, 45],
            Abilities =
            [
                Ability.Damage,
                Ability.Freeze,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Freeze,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }
}

