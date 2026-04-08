using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class MortarAndPestle
{
    private static Formula IsLifeStealWeapon { get; } =
        Condition.WithTag(Tag.Weapon)
        & Formula.Apply(Formula.Item(Key.LifeSteal), n => n != 0 ? 1 : 0);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "研钵与研杵",
            Desc = "▶吸血武器伤害提高 {Custom_0}（限本场战斗）；此物品右侧的武器获得吸血",
            Cooldown = 7.0,
            Tags = Tag.Tool,
            Custom_0 = [10, 20, 30, 40],
            Abilities =
            [
                Ability.AddAttribute(Key.Damage).Override(
                    valueKey: Key.Custom_0,
                    additionalTargetCondition: Condition.SameSide & IsLifeStealWeapon
                ),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.LifeSteal,
                    Condition = Condition.RightOfCaster & Condition.WithTag(Tag.Weapon),
                    Value = Formula.Constant(1),
                },
            ],
        };
    }
}

