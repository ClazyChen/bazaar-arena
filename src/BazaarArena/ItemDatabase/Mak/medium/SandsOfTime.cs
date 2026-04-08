using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class SandsOfTime
{
    private static Formula QuestBit(int bitIndex) =>
        Formula.Apply(Formula.Caster(Key.Quest), v => (v & (1 << (bitIndex - 1))) != 0 ? 1 : 0);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "时间之砂",
            Desc =
                "▶减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；【Q1】此物品 +{Custom_0} 多重释放；【Q2】此物品的冷却时间缩短 {Custom_1} 秒；【Q3】此物品 +{Custom_2} 减速目标数量",
            Cooldown = 6.0,
            Tags = Tag.Relic,
            Slow = 1.0,
            SlowTargetCount = [1, 2, 3, 4],
            Custom_0 = 1,
            Custom_1 = 1,
            Custom_2 = 1,
            Quest = 0,
            OverridableAttributes = new Dictionary<int, IntOrByTier>
            {
                [Key.Quest] = [0, 1, 3, 7],
            },
            Abilities =
            [
                Ability.Slow.Override(targetCountKey: Key.SlowTargetCount),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Condition = Condition.SameAsCaster,
                    Value = QuestBit(1) * Formula.Caster(Key.Custom_0),
                },
                new AuraDefinition
                {
                    Attribute = Key.CooldownMs,
                    Condition = Condition.SameAsCaster,
                    Value = QuestBit(2) * Formula.Constant(-1000),
                },
                new AuraDefinition
                {
                    Attribute = Key.SlowTargetCount,
                    Condition = Condition.SameAsCaster,
                    Value = QuestBit(3) * Formula.Caster(Key.Custom_2),
                },
            ],
        };
    }
}

