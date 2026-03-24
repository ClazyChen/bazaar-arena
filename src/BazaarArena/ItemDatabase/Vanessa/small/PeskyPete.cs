using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>鹦鹉皮特（Pesky Pete）及其历史版本：海盗小型伙伴；造成灼烧，每有一个相邻的伙伴或地产则 +1 多重释放；S1 另有「每有一个相邻伙伴或地产此物品获得 4/8 灼烧」光环。</summary>
public static class PeskyPete
{
    private static readonly Formula AdjacentFriendOrProperty =
        Condition.AdjacentToCaster & (Condition.WithTag(Tag.Friend) | Condition.WithTag(Tag.Property));

    /// <summary>鹦鹉皮特（最新版，铜）：8 » 7 » 6 » 5s 小 铜 伙伴；▶ 造成 2 灼烧；每有一个相邻的伙伴或地产，此物品 +1 多重释放。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "鹦鹉皮特",
            Desc = "▶ 造成 {Burn} 灼烧；每有一个相邻的伙伴或地产，此物品 +1 多重释放",
            Tags = [Tag.Friend],
            Cooldown = [8.0, 7.0, 6.0, 5.0],
            Burn = 2,
            Abilities = [Ability.Burn],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Value = Formula.Count(AdjacentFriendOrProperty),
                },
            ],
        };
    }

    /// <summary>鹦鹉皮特_S5（银）：7s 小 银 伙伴；▶ 造成 2 » 4 » 6 灼烧；每有一个相邻的伙伴或地产，此物品 +1 多重释放。</summary>
    public static ItemTemplate Template_S5()
    {
        return new ItemTemplate
        {
            Name = "鹦鹉皮特_S5",
            Desc = "▶ 造成 {Burn} 灼烧；每有一个相邻的伙伴或地产，此物品 +1 多重释放",
            Tags = [Tag.Friend],
            Cooldown = 7.0,
            Burn = [2, 4, 6],
            Abilities = [Ability.Burn],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Value = Formula.Count(AdjacentFriendOrProperty),
                },
            ],
        };
    }

    /// <summary>鹦鹉皮特_S1（金）：7s 小 金 伙伴；▶ 造成 4 » 6 灼烧；每有一个相邻的伙伴或地产，此物品 +1 多重释放、并获得 4 » 8 灼烧。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "鹦鹉皮特_S1",
            Desc = "▶ 造成 {Burn} 灼烧；每有一个相邻的伙伴或地产，此物品 +1 多重释放、并获得 {Custom_0} 灼烧",
            Tags = [Tag.Friend],
            Cooldown = 7.0,
            Burn = [4, 6],
            Custom_0 = [4, 8],
            Abilities = [Ability.Burn],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Value = Formula.Count(AdjacentFriendOrProperty),
                },
                new AuraDefinition
                {
                    Attribute = Key.Burn,
                    Value = Formula.Caster(Key.Custom_0) * Formula.Count(AdjacentFriendOrProperty),
                },
            ],
        };
    }
}
