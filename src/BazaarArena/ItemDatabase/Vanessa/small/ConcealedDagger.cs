using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>藏刃匕首（Concealed Dagger）及其历史版本：海盗小型武器。</summary>
public static class ConcealedDagger
{
    /// <summary>藏刃匕首：4s 小 铜 武器；▶ 造成 10 » 20 » 30 » 40 伤害；▶ 加速 1 件物品 1 » 2 » 3 » 4 秒（High）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "藏刃匕首",
            Desc = "▶ 造成 {Damage} 伤害；▶ 加速 {HasteTargetCount} 件物品 {HasteSeconds} 秒；战斗开始时，获得 {Gold} 金币",
            Tags = [Tag.Weapon],
            Cooldown = 4.0,
            Damage = [10, 20, 30, 40],
            Haste = [1.0, 2.0, 3.0, 4.0],
            Gold = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.Haste.Override(
                    priority: AbilityPriority.High
                ),
                Ability.GainGold.Override(
                    trigger: Trigger.BattleStart
                )
            ],
            DownstreamRequirements =
            [
                Synergy.And(Tag.Cooldown),
            ],
        };
    }

    /// <summary>藏刃匕首_S1（银）：9s 小 银 武器；▶ 造成 30 » 40 » 50 伤害。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "藏刃匕首_S1",
            Desc = "▶ 造成 {Damage} 伤害；▶ 获得 {Gold} 金币",
            Tags = [Tag.Weapon],
            Cooldown = 9.0,
            Damage = [30, 40, 50],
            Gold = [1, 2, 3],
            Abilities =
            [
                Ability.Damage,
                Ability.GainGold
            ],
        };
    }
}
