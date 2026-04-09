using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class CauterizingBlade
{
    private static Formula QuestActive(int questIndex) =>
        Formula.Apply(Formula.Caster(Key.Custom_1), n => n == questIndex ? 1 : 0);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "烙刀",
            Desc =
                "▶造成 {Damage} 伤害；▶造成 {Burn} 灼烧；【Q1】触发减速时，此物品伤害提高 {Custom_0}，灼烧提高 {Custom_2}（限本场战斗）；【Q2】触发加速时，此物品伤害提高 {Custom_0}，灼烧提高 {Custom_2}（限本场战斗）；【Q1】和【Q2】只能生效 1 个",
            Cooldown = 5.0,
            Tags = Tag.Weapon | Tag.Tech,
            Damage = 20,
            Burn = 2,
            Custom_0 = [10, 20, 30],
            Custom_1 = 0,
            Custom_2 = [2, 4, 6],
            OverridableAttributes = new Dictionary<int, IntOrByTier>
            {
                [Key.Custom_1] = 0,
            },
            Abilities =
            [
                Ability.Damage,
                Ability.Burn,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Slow,
                    additionalCondition: QuestActive(1),
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0
                ),
                Ability.AddAttribute(Key.Burn).Override(
                    trigger: Trigger.Slow,
                    additionalCondition: QuestActive(1),
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_2
                ),
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Haste,
                    additionalCondition: QuestActive(2),
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0
                ),
                Ability.AddAttribute(Key.Burn).Override(
                    trigger: Trigger.Haste,
                    additionalCondition: QuestActive(2),
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_2
                ),
            ],
        };
    }
}

