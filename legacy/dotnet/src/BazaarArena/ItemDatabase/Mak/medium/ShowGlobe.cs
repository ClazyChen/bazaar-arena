using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class ShowGlobe
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "展示玻璃球",
            Desc = "▶造成 {Burn} 灼烧；▶获得 {Regen} 生命再生；使用药水时，此物品的灼烧提高 {Custom_0}（限本场战斗）；使用药水时，此物品的生命再生提高 {Custom_0}（限本场战斗）",
            Cooldown = 7.0,
            Tags = 0,
            Burn = 2,
            Regen = 2,
            Custom_0 = [2, 4, 8, 12],
            Abilities =
            [
                Ability.Burn,
                Ability.Regen,
                Ability.AddAttribute(Key.Burn).Override(
                    trigger: Trigger.UseOtherItem,
                    condition: Condition.SameSide & Condition.DifferentFromCaster & Condition.WithTag(Tag.Potion),
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0
                ),
                Ability.AddAttribute(Key.Regen).Override(
                    trigger: Trigger.UseOtherItem,
                    condition: Condition.SameSide & Condition.DifferentFromCaster & Condition.WithTag(Tag.Potion),
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0
                ),
            ],
        };
    }
}

