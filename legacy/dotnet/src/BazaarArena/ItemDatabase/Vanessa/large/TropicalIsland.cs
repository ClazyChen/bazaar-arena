using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

/// <summary>热带岛屿（Tropical Island）：海盗大型水系、地产；触发减速时获得生命再生。「每小时椰子/柑橘」为局外成长，模拟器不实现。</summary>
public static class TropicalIsland
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "热带岛屿",
            Desc = "触发减速时，获得 {Regen} 生命再生（限本场战斗）；每小时开始时，获得 1 个椰子或柑橘",
            Tags = Tag.Aquatic | Tag.Property,
            Cooldown = 0.0,
            Regen = [5, 10],
            Abilities =
            [
                Ability.Regen.Override(
                    trigger: Trigger.Slow,
                    priority: AbilityPriority.Medium),
            ],
        };
    }
}
