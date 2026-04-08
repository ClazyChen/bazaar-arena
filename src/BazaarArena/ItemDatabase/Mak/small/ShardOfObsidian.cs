using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class ShardOfObsidian
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "黑曜石碎片",
            Desc = "▶造成 {Damage} 伤害；吸血",
            Cooldown = 5.0,
            Tags = Tag.Weapon | Tag.Reagent,
            Damage = [5, 10, 20, 40],
            LifeSteal = 1,
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }
}

