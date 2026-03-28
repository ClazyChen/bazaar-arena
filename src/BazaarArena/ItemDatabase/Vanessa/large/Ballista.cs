using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

/// <summary>弩炮（Ballista）：海盗大型武器。</summary>
public static class Ballista
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "弩炮",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；使用其他弹药物品时，此物品的多重释放提高 {Custom_0}（限本场战斗）",
            Tags = Tag.Weapon,
            Cooldown = 9.0,
            Damage = [150, 200],
            AmmoCap = 2,
            Custom_0 = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Multicast).Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.WithDerivedTag(DerivedTag.Ammo),
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Medium),
            ],
        };
    }
}
