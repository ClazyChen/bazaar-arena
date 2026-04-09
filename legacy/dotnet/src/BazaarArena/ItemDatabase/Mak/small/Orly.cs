using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class Orly
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "猫头鹰奥利",
            Desc = "造成暴击时，{Custom_0} 件物品开始飞行；飞行物品 +{Custom_1%} 暴击率",
            Cooldown = 0.0,
            Tags = Tag.Friend,
            Custom_0 = 1,
            Custom_1 = [20, 30, 40, 50],
            Custom_2 = 1,
            Abilities =
            [
                Ability.StartFlying.Override(
                    trigger: Trigger.Crit,
                    targetCountKey: Key.Custom_0,
                    valueKey: Key.Custom_2
                ),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Condition = Condition.SameSide & Condition.InFlight,
                    Value = Formula.Caster(Key.Custom_1),
                },
            ],
        };
    }
}

