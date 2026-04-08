using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class QuillAndInk
{
    private static Formula OnlyWeaponSelf { get; } = Condition.OnlyWeapon & Condition.SameAsCaster;

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "笔与墨",
            Desc = "▶造成 {Poison} 剧毒；▶获得 {Regen} 生命再生；如果没有其他武器，此物品 {+Custom_0} 多重触发",
            Cooldown = [7.0, 6.0, 5.0, 4.0],
            Tags = Tag.Tool,
            Poison = 1,
            Regen = 1,
            Custom_0 = 1,
            Multicast = 1,
            Abilities =
            [
                Ability.Poison,
                Ability.Regen.Override(priority: AbilityPriority.Low),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Condition = Condition.SameAsCaster,
                    Value = Formula.Constant(1) + (Formula.Caster(Key.Custom_0) * OnlyWeaponSelf),
                },
            ],
        };
    }
}

