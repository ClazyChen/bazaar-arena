using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class SleepingPotion
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "昏睡药水",
            Desc = "▶减速 {SlowTargetCount} 件敌方冷却时间最长的物品 {SlowSeconds} 秒；弹药：{AmmoCap}",
            Cooldown = 4.0,
            Tags = Tag.Potion,
            AmmoCap = 1,
            Slow = [3.0, 4.0, 5.0, 6.0],
            SlowTargetCount = 1,
            Abilities =
            [
                Ability.Slow.Override(
                    targetCondition: Condition.DifferentSide & Condition.HasCooldown & Condition.OppHighestCooldown,
                    targetCountKey: Key.SlowTargetCount
                ),
            ],
        };
    }
}

