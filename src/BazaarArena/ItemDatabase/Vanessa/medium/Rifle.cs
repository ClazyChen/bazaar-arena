using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>步枪（Rifle）：海盗中型武器。▶ 造成伤害；▶ 此物品伤害提高（限本场战斗）；弹药 1 发。或（历史版）造成伤害、若为唯一有冷却的武器则装填 1 弹药。</summary>
public static class Rifle
{
    /// <summary>步枪（最新版）：3s 中 铜 武器；▶ 造成 20 » 40 » 60 » 80 伤害；▶ 此物品的伤害提高 20 » 40 » 60 » 80（限本场战斗）（L）；弹药：1。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "步枪",
            Desc = "▶ 造成 {Damage} 伤害；▶ 此物品的伤害提高 {Custom_0}（限本场战斗）；弹药：{AmmoCap}",
            Tags = [Tag.Weapon],
            Cooldown = 3.0,
            Damage = [20, 40, 60, 80],
            Custom_0 = [20, 40, 60, 80],
            AmmoCap = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    targetCondition: Condition.SameAsSource,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }

    /// <summary>步枪_S1（版本 1）：3s 中 铜 武器；▶ 造成 8 » 16 » 30 » 48 伤害；若这是你唯一有冷却时间的武器，为此物品装填 1 弹药（Lst）；弹药：1。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "步枪_S1",
            Desc = "▶ 造成 {Damage} 伤害；若这是你唯一有冷却时间的武器，为此物品装填 1 弹药；弹药：{AmmoCap}",
            Tags = [Tag.Weapon],
            Cooldown = 3.0,
            Damage = [8, 16, 30, 48],
            Custom_0 = 1,
            AmmoCap = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.Reload.Override(
                    condition: Condition.SameAsSource & Condition.OnlyWeaponWithCooldown,
                    targetCondition: Condition.SameAsSource,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Lowest
                ),
            ],
        };
    }
}
