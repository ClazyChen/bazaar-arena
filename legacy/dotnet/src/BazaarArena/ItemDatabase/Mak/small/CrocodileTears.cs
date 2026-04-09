using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class CrocodileTears
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "鳄鱼眼泪",
            Desc = "▶造成 {Damage} 伤害；弹药：{AmmoCap}；敌方生命值降低时，此物品的伤害提高 {Custom_0}（限本场战斗）",
            Cooldown = [13.0, 12.0, 11.0, 10.0],
            Tags = Tag.Weapon | Tag.Potion,
            Damage = 1,
            AmmoCap = 1,
            Custom_0 = [8, 16, 24, 32],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.OppHpReduced,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }
}

