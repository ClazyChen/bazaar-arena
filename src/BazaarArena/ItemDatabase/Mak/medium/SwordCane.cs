using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class SwordCane
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "剑杖",
            Desc = "▶造成 {Damage} 伤害；▶如果与生命再生物品相邻，获得 {Regen} 生命再生；▶如果与灼烧物品相邻，造成 {Burn} 灼烧；▶如果与剧毒物品相邻，造成 {Poison} 剧毒",
            Cooldown = [5.0, 4.0, 3.0, 2.0],
            Tags = Tag.Weapon,
            Damage = 20,
            Regen = 2,
            Burn = 2,
            Poison = 2,
            Abilities =
            [
                Ability.Damage,
                Ability.Regen.Override(
                    additionalCondition: Formula.Apply(
                        Formula.Count(Condition.SameSide & Condition.AdjacentToCaster & Condition.WithDerivedTag(DerivedTag.Regen)),
                        n => n > 0 ? 1 : 0),
                    priority: AbilityPriority.Low
                ),
                Ability.Burn.Override(
                    additionalCondition: Formula.Apply(
                        Formula.Count(Condition.SameSide & Condition.AdjacentToCaster & Condition.WithDerivedTag(DerivedTag.Burn)),
                        n => n > 0 ? 1 : 0)
                ),
                Ability.Poison.Override(
                    additionalCondition: Formula.Apply(
                        Formula.Count(Condition.SameSide & Condition.AdjacentToCaster & Condition.WithDerivedTag(DerivedTag.Poison)),
                        n => n > 0 ? 1 : 0)
                ),
            ],
        };
    }
}

