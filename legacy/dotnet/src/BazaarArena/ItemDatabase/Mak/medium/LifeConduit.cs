using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class LifeConduit
{
    private static Formula QuestBit(int bitIndex) =>
        Formula.Apply(Formula.Caster(Key.Quest), v => (v & (1 << (bitIndex - 1))) != 0 ? 1 : 0);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "生命导体",
            Desc =
                "▶获得 {Regen} 生命再生；【Q1】触发生命再生时，此物品的生命再生提高 {Custom_0}（限本场战斗）；【Q2】此物品 +{Custom_1} 多重释放；【Q3】此物品的冷却时间缩短 {Custom_2} 秒",
            Cooldown = 6.0,
            Tags = Tag.Relic,
            Regen = [2, 4, 6, 8],
            Custom_0 = [2, 4, 6, 8],
            Custom_1 = 1,
            Custom_2 = 1,
            Quest = 0,
            OverridableAttributes = new Dictionary<int, IntOrByTier>
            {
                [Key.Quest] = [0, 1, 3, 7],
            },
            Abilities =
            [
                Ability.Regen,
                Ability.AddAttribute(Key.Regen).Override(
                    trigger: Trigger.Regen,
                    additionalCondition: QuestBit(1),
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low
                ),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Condition = Condition.SameAsCaster,
                    Value = QuestBit(2) * Formula.Caster(Key.Custom_1),
                },
                new AuraDefinition
                {
                    Attribute = Key.CooldownMs,
                    Condition = Condition.SameAsCaster,
                    Value = QuestBit(3) * (-1000 * Formula.Caster(Key.Custom_2)),
                },
            ],
        };
    }
}

