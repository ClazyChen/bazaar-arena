using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class Candles
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "蜡烛",
            Desc = "▶造成 {Burn} 灼烧；使用小型物品时，为此物品充能 {ChargeSeconds} 秒",
            Cooldown = 9.0,
            Tags = 0,
            Burn = [8, 12, 16, 20],
            Charge = 2.0,
            Abilities =
            [
                Ability.Burn,
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.WithTag(Tag.Small),
                    targetCondition: Condition.SameAsCaster
                ),
            ],
        };
    }
}

