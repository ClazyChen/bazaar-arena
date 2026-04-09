using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class ElementalDepthCharge
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "元素深水炸弹",
            Desc = "▶ 造成 {Burn} 灼烧；▶ 造成 {Poison} 剧毒；▶ 冻结 1 件物品 {FreezeSeconds} 秒；弹药：{AmmoCap}；每有 1 件其他水系物品，此物品 +1 多重释放",
            Cooldown = [12.0, 10.0, 8.0],
            Tags = Tag.Aquatic | Tag.Tech,
            Burn = 4,
            Poison = 4,
            Freeze = 0.5,
            AmmoCap = 1,
            Abilities =
            [
                Ability.Burn,
                Ability.Poison,
                Ability.Freeze,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Value = Formula.Count(Condition.SameSide & Condition.DifferentFromCaster & Condition.WithTag(Tag.Aquatic)),
                }
            ],
        };
    }
}

