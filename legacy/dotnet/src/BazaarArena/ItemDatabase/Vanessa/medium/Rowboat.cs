using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>划艇（Rowboat）：海盗中型水系、载具；己方未摧毁物品的「主类型」标签（不含尺寸）种类数 ≥7 时冷却缩短 5 秒。</summary>
public static class Rowboat
{
    private static readonly Formula AtLeastSevenDistinctPrimaryTagsOnSide = new(ctx =>
    {
        const int sizeMask = Tag.Small | Tag.Medium | Tag.Large;
        int seen = 0;
        int count = 0;
        var side = ctx.BattleState.Side[ctx.Caster.SideIndex];
        foreach (var it in side.Items)
        {
            if (it.Destroyed) continue;
            int tags = ctx.BattleState.GetItemInt(it, Key.Tags) & ~sizeMask;
            for (int bits = tags; bits != 0; bits &= bits - 1)
            {
                int bit = bits & -bits;
                if ((seen & bit) != 0) continue;
                seen |= bit;
                count++;
            }
        }
        return count >= 7 ? 1 : 0;
    });

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "划艇",
            Desc = "▶ 为相邻物品充能 {ChargeSeconds} 秒；如果己方物品有至少 7 种独特类型，此物品的冷却时间缩短 5 秒",
            Tags = Tag.Aquatic | Tag.Vehicle,
            Cooldown = 8.0,
            Charge = [1.0, 2.0],
            ChargeTargetCount = 2,
            Abilities =
            [
                Ability.Charge.Override(
                    additionalTargetCondition: Condition.AdjacentToCaster,
                    priority: AbilityPriority.High),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CooldownMs,
                    Condition = Condition.SameAsCaster & AtLeastSevenDistinctPrimaryTagsOnSide,
                    Value = Formula.Constant(-5000),
                },
            ],
        };
    }
}
