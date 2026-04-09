using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class LetterOpener
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "拆信刀",
            Desc = "▶造成 {Damage} 伤害；▶此物品的暴击率降低 {Custom_0%}（限本场战斗）；暴击率：{CritRate%}",
            Cooldown = 5.0,
            Tags = Tag.Weapon | Tag.Tool,
            Damage = [10, 20, 40, 80],
            CritRate = [100, 125, 150, 175],
            Custom_0 = 25,
            Abilities =
            [
                Ability.Damage,
                Ability.ReduceAttribute(Key.CritRate).Override(
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0
                ),
            ],
        };
    }
}

