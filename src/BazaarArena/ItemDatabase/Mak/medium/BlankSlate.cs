using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class BlankSlate
{
    private static Formula QuestBit(int bitIndex) =>
        Formula.Apply(Formula.Caster(Key.Quest), v => (v & (1 << (bitIndex - 1))) != 0 ? 1 : 0);

    private static Formula AdjacentRelicUsed { get; } =
        Condition.SameSide & Condition.DifferentFromCaster & Condition.WithTag(Tag.Relic) & Condition.AdjacentToCaster;

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "空白石碑",
            Desc =
                "使用相邻遗物时，为此物品充能 {ChargeSeconds} 秒；【Q1】▶造成 {Poison} 剧毒；【Q2】▶获得 {Regen} 生命再生；【Q3】▶造成 {Burn} 灼烧；【Q4】▶减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；【Q5】▶冻结 {FreezeTargetCount} 件物品 {FreezeSeconds} 秒",
            Cooldown = 6.0,
            Tags = Tag.Relic,
            Charge = 1.0,
            Poison = [5, 10, 15, 20],
            Regen = [5, 10, 15, 20],
            Burn = [5, 10, 15, 20],
            Slow = 1.0,
            SlowTargetCount = [1, 2, 3, 4],
            Freeze = 1.0,
            Quest = 0,
            OverridableAttributes = new Dictionary<int, IntOrByTier>
            {
                [Key.Quest] = [0, 1, 7, 31],
            },
            Abilities =
            [
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    condition: AdjacentRelicUsed,
                    targetCondition: Condition.SameAsCaster
                ),
                Ability.Poison.Override(additionalCondition: QuestBit(1), priority: AbilityPriority.Low),
                Ability.Regen.Override(additionalCondition: QuestBit(2), priority: AbilityPriority.Low),
                Ability.Burn.Override(additionalCondition: QuestBit(3), priority: AbilityPriority.Low),
                Ability.Slow.Override(additionalCondition: QuestBit(4), targetCountKey: Key.SlowTargetCount, priority: AbilityPriority.Low),
                Ability.Freeze.Override(additionalCondition: QuestBit(5), priority: AbilityPriority.Low),
            ],
        };
    }
}

