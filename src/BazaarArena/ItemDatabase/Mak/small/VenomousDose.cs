using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class VenomousDose
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "毒液注射",
            Desc = "▶造成 {Poison} 剧毒；▶对自己造成 {Poison} 剧毒；▶获得生命再生，等量于此物品的剧毒",
            Cooldown = 5.0,
            Tags = 0,
            Poison = [2, 4, 6, 8],
            Abilities =
            [
                Ability.Poison,
                Ability.PoisonSelf,
                Ability.Regen,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Regen,
                    Value = Formula.Caster(Key.Poison),
                },
            ],
        };
    }
}

