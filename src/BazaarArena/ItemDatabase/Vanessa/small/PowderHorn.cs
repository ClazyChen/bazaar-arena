using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>火药角（Powder Horn）：海盗小型工具；▶ 为此物品右侧的物品装填 1 » 2 » 3 » 4 弹药。</summary>
public static class PowderHorn
{
    /// <summary>火药角（最新版）：4s 小 铜 工具；▶ 为此物品右侧的物品装填 {Custom_0} 弹药。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "火药角",
            Desc = "▶ 为此物品右侧的物品装填 {Custom_0} 弹药",
            Tags = [Tag.Tool],
            Cooldown = 4.0,
            Custom_0 = [1, 2, 3, 4],
            Abilities =
            [
                Ability.Reload.Override(
                    additionalTargetCondition: Condition.RightOfCaster,
                    priority: AbilityPriority.Lowest
                ),
            ],
        };
    }
}
