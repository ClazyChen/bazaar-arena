using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Lockbox
{
    private static Formula EffectiveValue { get; } =
        Formula.Caster(Key.Value) + Formula.Caster(Key.Custom_1) * Formula.Caster(Key.Custom_2);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "锁匣",
            Desc = "赢得战斗时，此物品的价值永久提升 {Custom_2}；武器 +伤害，等量于此物品的价值",
            Cooldown = 0.0,
            Tags = Tag.Relic,
            Custom_1 = 0,
            Custom_2 = [3, 6, 9],
            OverridableAttributes = new Dictionary<int, IntOrByTier>
            {
                [Key.Custom_1] = [0, 3, 6],
            },
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Damage,
                    Condition = Condition.SameSide & Condition.WithTag(Tag.Weapon),
                    Value = EffectiveValue,
                },
            ],
        };
    }
}

