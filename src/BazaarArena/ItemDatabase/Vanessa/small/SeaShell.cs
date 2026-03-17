using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>海螺壳（Sea Shell）：海盗小型水系；▶ 每拥有一件水系物品，获得 10 » 15 » 20 » 25 护盾（护盾量 = Custom_0 × 己方水系物品数）。</summary>
public static class SeaShell
{
    /// <summary>海螺壳：6s 小 铜 水系；▶ 每拥有一件水系物品，获得 {Custom_0} 护盾。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "海螺壳",
            Desc = "▶ 每拥有一件水系物品，获得 {Custom_0} 护盾",
            Tags = [Tag.Aquatic],
            Cooldown = 6.0,
            Custom_0 = [10, 15, 20, 25],
            Abilities =
            [
                Ability.Shield,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    AttributeName = Key.Shield,
                    Condition = Condition.SameAsSource,
                    Value = Formula.Source(Key.Custom_0) * Formula.Count(Condition.SameSide & Condition.WithTag(Tag.Aquatic)),
                },
            ],
            NeighborPreference =
            [
                Synergy.And(Tag.Aquatic),
            ],
        };
    }
}
