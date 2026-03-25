using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Disguise
{
    private static Formula SameHeroAsCaster { get; } = new(ctx =>
        ctx.GetItemInt(ctx.Item, Key.Hero) == ctx.GetItemInt(ctx.Caster, Key.Hero) ? 1 : 0);
    private static Formula DifferentHeroFromCaster { get; } = new(ctx =>
        ctx.GetItemInt(ctx.Item, Key.Hero) != ctx.GetItemInt(ctx.Caster, Key.Hero) ? 1 : 0);

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "伪装",
            Desc = "使用其他英雄的物品时，为己方英雄的 {ChargeTargetCount} 件物品充能 {Charge} 秒；购买此物品时，获得 1 件其他英雄的小型或中型物品",
            Cooldown = 0.0,
            Tags = Tag.Apparel,
            Charge = 1.0,
            ChargeTargetCount = [1, 2, 3],
            Abilities =
            [
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: DifferentHeroFromCaster,
                    additionalTargetCondition: SameHeroAsCaster & ~Condition.Destroyed),
            ],
        };
    }

    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "伪装_S1",
            Desc = "其他英雄的物品 {+Custom_0%} 暴击率；购买此物品时，获得 1 件其他英雄的物品",
            Cooldown = 0.0,
            Tags = Tag.Apparel,
            Custom_0 = [15, 30, 50],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Condition = Condition.SameSide & DifferentHeroFromCaster & Condition.CanCrit,
                    Value = Formula.Caster(Key.Custom_0),
                }
            ],
        };
    }
}

