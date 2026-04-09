using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class BarbedClaws
{
    private static Formula SidePoisoned { get; } =
        Formula.Apply(Formula.Side(Key.Poison), n => n > 0 ? 1 : 0);

    private static Formula OppPoisoned { get; } =
        Formula.Apply(Formula.Opp(Key.Poison), n => n > 0 ? 1 : 0);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "倒刺利爪",
            Desc = "▶造成 {Damage} 伤害；如果敌方承受剧毒，此物品 +1 多重释放；如果己方承受剧毒，此物品 +1 多重释放",
            Cooldown = 6.0,
            Tags = Tag.Weapon,
            Damage = [5, 10, 20, 40],
            Multicast = 1,
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Value = Formula.Constant(1) + OppPoisoned + SidePoisoned,
                },
            ],
        };
    }
}

