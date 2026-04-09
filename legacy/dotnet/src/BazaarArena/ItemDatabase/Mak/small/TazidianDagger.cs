using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class TazidianDagger
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "塔兹迪亚匕首",
            Desc = "▶造成 {Damage} 伤害；此物品左侧的药水 {+Custom_0} 最大弹药量",
            Cooldown = 6.0,
            Tags = Tag.Weapon | Tag.Relic,
            Damage = [10, 20, 30, 40],
            Custom_0 = [1, 2, 3, 4],
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.AmmoCap,
                    Condition = Condition.LeftOfCaster & Condition.WithTag(Tag.Potion),
                    Value = Formula.Caster(Key.Custom_0),
                },
            ],
        };
    }
}

