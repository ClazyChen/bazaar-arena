using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class BottledLightning
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "瓶装闪电",
            Desc = "▶造成 {Damage} 伤害；▶造成 {Burn} 灼烧；弹药：{AmmoCap}；暴击率：{CritRate%}",
            Cooldown = 6.0,
            Tags = Tag.Weapon | Tag.Potion,
            Damage = [15, 30, 60, 120],
            Burn = [1, 2, 3, 4],
            AmmoCap = 1,
            CritRate = 100,
            Abilities =
            [
                Ability.Damage,
                Ability.Burn,
            ],
        };
    }
}

