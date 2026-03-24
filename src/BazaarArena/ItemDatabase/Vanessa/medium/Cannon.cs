using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>火炮（Cannon）：海盗中型武器。▶ 造成伤害；▶ 造成灼烧，等量于此物品伤害的 10%；弹药 2 发。</summary>
public static class Cannon
{
    /// <summary>火炮：4s 中 铜 武器；▶ 造成 40 » 60 » 80 » 100 伤害（Hst）；▶ 造成灼烧，等量于此物品伤害的 10%（H）；弹药：2。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "火炮",
            Desc = "▶ 造成 {Damage} 伤害；▶ 造成灼烧，等量于此物品伤害的 10%；弹药：{AmmoCap}",
            Tags = Tag.Weapon,
            Cooldown = 4.0,
            Damage = [40, 60, 80, 100],
            AmmoCap = 2,
            Abilities =
            [
                Ability.Damage.Override(priority: AbilityPriority.Highest),
                Ability.Burn.Override(priority: AbilityPriority.High),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Burn,
                    Value = RatioUtil.PercentFloor(Formula.Caster(Key.Damage), 10),
                },
            ],
        };
    }
}
