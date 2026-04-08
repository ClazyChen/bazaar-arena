using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class Leeches
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "水蛭",
            Desc = "▶造成 {Damage} 伤害；触发剧毒时，此物品的伤害提高 {Custom_0}（限本场战斗）；吸血",
            Cooldown = 8.0,
            Tags = Tag.Weapon | Tag.Friend,
            Damage = 20,
            LifeSteal = 1,
            Custom_0 = [10, 20, 30, 40],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Poison,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }
}

