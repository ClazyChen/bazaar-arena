using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class BasiliskFang
{
    private static Formula OppPoisoned { get; } =
        Formula.Apply(Formula.Opp(Key.Poison), n => n > 0 ? 1 : 0);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "蛇怪之牙",
            Desc = "▶造成 {Damage} 伤害；如果敌方承受剧毒，此物品 +{Custom_0%} 暴击率；吸血",
            Cooldown = 4.0,
            Tags = Tag.Weapon | Tag.Relic,
            Damage = [10, 20, 40, 80],
            LifeSteal = 1,
            Custom_0 = [25, 50, 75, 100],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Condition = Condition.SameAsCaster,
                    Value = OppPoisoned * Formula.Caster(Key.Custom_0),
                },
            ],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }
}

