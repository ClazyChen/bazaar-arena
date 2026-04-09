using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

/// <summary>灯塔（Lighthouse）：海盗大型水系、地产。</summary>
public static class Lighthouse
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "灯塔",
            Desc = "▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；触发减速时，造成 {Burn} 灼烧",
            Tags = Tag.Aquatic | Tag.Property,
            Cooldown = 6.0,
            Slow = 2.0,
            SlowTargetCount = [1, 2],
            Burn = [6, 9],
            Abilities =
            [
                Ability.Slow.Override(priority: AbilityPriority.Low),
                Ability.Burn.Override(
                    trigger: Trigger.Slow,
                    priority: AbilityPriority.Low),
            ],
        };
    }
}
