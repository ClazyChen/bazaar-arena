using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class RainbowPotion
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "彩虹药水",
            Desc =
                "▶造成 {Burn} 灼烧；▶造成 {Poison} 剧毒；▶冻结 {FreezeTargetCount} 件物品 {FreezeSeconds} 秒；▶减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；弹药：{AmmoCap}",
            Cooldown = 6.0,
            Tags = Tag.Potion,
            AmmoCap = 1,
            Burn = [3, 6, 9, 12],
            Poison = [3, 6, 9, 12],
            Freeze = 1.0,
            Slow = 2.0,
            SlowTargetCount = 1,
            Abilities =
            [
                Ability.Burn,
                Ability.Poison,
                Ability.Freeze,
                Ability.Slow.Override(targetCountKey: Key.SlowTargetCount),
            ],
        };
    }
}

