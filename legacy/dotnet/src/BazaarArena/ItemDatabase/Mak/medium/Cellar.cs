using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class Cellar
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "地窖",
            Desc = "▶装填 {ReloadTargetCount} 件物品；▶获得 {Regen} 生命再生",
            Cooldown = 3.0,
            Tags = Tag.Property,
            Reload = 1,
            ReloadTargetCount = [1, 2, 3, 4],
            Regen = [1, 2, 4, 8],
            Abilities =
            [
                Ability.Reload.Override(
                    priority: AbilityPriority.Lowest,
                    targetCountKey: Key.ReloadTargetCount
                ),
                Ability.Regen,
            ],
        };
    }
}

