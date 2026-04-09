using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

/// <summary>滚石（The Boulder）：海盗大型武器、遗物、陷阱；伤害等于敌方最大生命值。</summary>
public static class TheBoulder
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "滚石",
            Desc = "▶ 造成伤害，等量于敌人最大生命值；弹药：{AmmoCap}",
            Tags = Tag.Weapon | Tag.Relic | Tag.Trap,
            Cooldown = [20.0, 16.0],
            Damage = 0,
            AmmoCap = 1,
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Damage,
                    Condition = Condition.SameAsCaster,
                    Value = Formula.Opp(Key.MaxHp),
                },
            ],
        };
    }
}
