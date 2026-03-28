using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>狙击步枪（Sniper Rifle）：海盗中型武器；唯一武器时伤害按百分比倍增（光环实现）。</summary>
public static class SniperRifle
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "狙击步枪",
            Desc = "▶ 造成 {Damage} 伤害；如果此物品是你唯一的武器，伤害额外提高 {+Custom_0%}",
            Tags = Tag.Weapon,
            Cooldown = 9.0,
            Damage = 100,
            Custom_0 = [400, 900],
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Damage,
                    Condition = Condition.OnlyWeapon,
                    Percent = true,
                    Value = Formula.Caster(Key.Custom_0),
                },
            ],
        };
    }
}
