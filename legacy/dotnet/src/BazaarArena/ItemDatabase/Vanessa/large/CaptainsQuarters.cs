using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

public static class CaptainsQuarters
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "船长舱",
            Desc = "▶ 加速工具和载具 {HasteSeconds} 秒；▶ 为弹药物品装填 {Reload} 弹药（限本场战斗）；▶ 武器伤害提高 {Custom_0}（限本场战斗）",
            Cooldown = 4.0,
            Tags = Tag.Aquatic | Tag.Property,
            Haste = [1.0, 2.0, 3.0],
            Reload = [1, 2, 3],
            Custom_0 = [20, 30, 40],
            Abilities =
            [
                Ability.Haste.Override(
                    additionalTargetCondition: Condition.WithTag(Tag.Tool | Tag.Vehicle)),
                Ability.Reload.Override(
                    additionalTargetCondition: Condition.WithDerivedTag(DerivedTag.Ammo),
                    priority: AbilityPriority.Low),
                Ability.AddAttribute(Key.Damage).Override(
                    additionalTargetCondition: Condition.WithTag(Tag.Weapon),
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low),
            ],
        };
    }
}

