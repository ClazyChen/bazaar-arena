using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class OniMask
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "恶鬼面具",
            Desc = "▶ 造成 {Burn} 灼烧；造成暴击时，灼烧物品的灼烧提高 {Custom_0}（限本场战斗）；触发减速时，为此物品充能 {ChargeSeconds} 秒",
            Cooldown = 10.0,
            Tags = Tag.Apparel | Tag.Tech,
            Burn = [6, 9, 12],
            Custom_0 = [4, 6, 8],
            Charge = 2.0,
            Abilities =
            [
                Ability.Burn,
                Ability.AddAttribute(Key.Burn).Override(
                    trigger: Trigger.Crit,
                    additionalTargetCondition: Condition.WithDerivedTag(DerivedTag.Burn),
                    valueKey: Key.Custom_0),
                Ability.Charge.Override(
                    trigger: Trigger.Slow,
                    targetCondition: Condition.SameAsCaster),
            ],
        };
    }
}

