using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class Peacewrought
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "和平铸箱",
            Desc =
                "▶获得 {Regen} 生命再生；▶摧毁物品时，获得 {Custom_1} 金币；【局外】拜访商人时，摧毁此物品左侧的物品，并将其价值加至此物品的生命再生；【默认】生命再生提高量：{Custom_0}",
            Cooldown = [7.0, 6.0, 5.0, 4.0],
            Tags = Tag.Relic,
            Regen = 4,
            Custom_0 = [6, 12, 18, 24],
            Custom_1 = [2, 4, 6, 8],
            OverridableAttributes = new Dictionary<int, IntOrByTier>
            {
                [Key.Custom_0] = [6, 12, 18, 24],
            },
            Abilities =
            [
                Ability.Regen,
                Ability.GainGold.Override(
                    trigger: Trigger.Destroy,
                    valueKey: Key.Custom_1,
                    priority: AbilityPriority.Low
                ),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Regen,
                    Value = Formula.Caster(Key.Custom_0),
                },
            ],
        };
    }
}

