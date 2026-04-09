using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>湿件战服（Wetware）：海盗中型水系、服饰、科技。▶ 获得护盾；使用武器时此物品护盾提高（限本场战斗）；若拥有另一件科技物品则此物品冷却缩短 2 秒。</summary>
public static class Wetware
{
    /// <summary>湿件战服：8s 中 银 水系 服饰 科技；▶ 获得 10 护盾；▶ 使用武器时此物品护盾提高 15 » 25 » 35 » 45（限本场战斗）；若拥有另一件科技物品则此物品冷却缩短 2 秒。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "湿件战服",
            Desc = "▶ 获得 {Shield} 护盾；▶ 使用武器时，此物品的护盾提高 {Custom_0}（限本场战斗）；如果有 1 件其他的科技物品，此物品的冷却时间缩短 2 秒",
            Tags = Tag.Aquatic | Tag.Apparel | Tag.Tech,
            Cooldown = 8.0,
            Shield = 10,
            Custom_0 = [15, 25, 35, 45],
            Abilities =
            [
                Ability.Shield,
                Ability.AddAttribute(Key.Shield).Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.WithTag(Tag.Weapon),
                    targetCondition: Condition.SameAsCaster
                ),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CooldownMs,
                    Value = Formula.Constant(-2000) * Formula.Apply(
                        Formula.Count(Condition.SameSide & Condition.WithTag(Tag.Tech) & Condition.DifferentFromCaster),
                        c => c > 0 ? 1 : 0),
                },
            ],
        };
    }
}
