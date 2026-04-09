using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class Scalpel
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "手术刀",
            Desc = "▶造成伤害，等量于此物品的暴击率；暴击率：{CritRate%}；造成暴击时，此物品的暴击率提高 {Custom_0%}（限本场战斗）",
            Cooldown = 5.0,
            Tags = Tag.Weapon | Tag.Tool,
            CritRate = 10,
            Custom_0 = [10, 15, 20, 25],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.CritRate).Override(
                    trigger: Trigger.Crit,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.High
                ),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Damage,
                    Value = Formula.Caster(Key.CritRate),
                },
            ],
        };
    }
}

