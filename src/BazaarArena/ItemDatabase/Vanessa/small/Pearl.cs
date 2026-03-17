using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>珍珠（Pearl）：海盗小型水系；▶ 获得 10 » 20 » 30 » 40 护盾；使用其他水系物品时，为此物品充能 1 秒。</summary>
public static class Pearl
{
    /// <summary>珍珠：5s 小 铜 水系；▶ 获得 {Shield} 护盾；使用其他水系物品时，为此物品充能 1 秒。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "珍珠",
            Desc = "▶ 获得 {Shield} 护盾；使用其他水系物品时，为此物品充能 1 秒",
            Tags = [Tag.Aquatic],
            Cooldown = 5.0,
            Shield = [10, 20, 30, 40],
            Charge = 1.0,
            Abilities =
            [
                Ability.Shield.Override(
                    priority: AbilityPriority.Low
                ),
                Ability.Charge.Override(
                    condition: Condition.SameSide & Condition.WithTag(Tag.Aquatic) & Condition.DifferentFromSource,
                    targetCondition: Condition.SameAsSource,
                    priority: AbilityPriority.Low
                ),
            ],
            UpstreamRequirements =
            [
                Synergy.And(Tag.Aquatic, Tag.Cooldown),
            ],
        };
    }
}
