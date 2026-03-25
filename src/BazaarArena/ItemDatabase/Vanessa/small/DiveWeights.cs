using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>潜水配重（Dive Weights）：海盗小型水系、工具、服饰；▶ 加速；弹药：4；相邻水系使冷却缩短；剩余弹药提供多重释放。</summary>
public static class DiveWeights
{
    /// <summary>潜水配重（版本 1，银）：8s 小 银 水系 工具 服饰；▶ 加速 1 件物品 1 » 2 » 3 秒；弹药：{AmmoCap}；每有 1 件相邻的水系物品，此物品冷却时间缩短 1 秒；每有 1 剩余弹药，此物品 +1 多重释放。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "潜水配重",
            Desc = "▶ 加速 {HasteTargetCount} 件物品 {HasteSeconds} 秒；弹药：{AmmoCap}；每有 1 件相邻的水系物品，此物品的冷却时间缩短 1 秒；每有 1 剩余弹药，此物品 +1 多重释放",
            Tags = Tag.Aquatic | Tag.Tool | Tag.Apparel,
            Cooldown = 8.0,
            AmmoCap = 4,
            Haste = [1.0, 2.0, 3.0],
            Abilities =
            [
                Ability.Haste,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CooldownMs,
                    Value = Formula.Constant(-1000) * Formula.Count(Condition.AdjacentToCaster & Condition.WithTag(Tag.Aquatic)),
                },
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Value = Formula.Caster(Key.AmmoRemaining),
                },
            ],
        };
    }
}

