using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

public static class Submarine
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "潜艇",
            Desc = "▶ 造成 {Damage} 伤害；▶ 获得护盾，等量于此物品的伤害；如果此物品是你唯一的武器，其受到冻结和减速的持续时间减半",
            Cooldown = [4.0, 3.0, 2.0],
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Vehicle | Tag.Tech,
            Damage = [60, 120, 240],
            Shield = 0,
            Abilities =
            [
                Ability.Damage,
                Ability.Shield,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Shield,
                    Value = Formula.Caster(Key.Damage),
                },
                new AuraDefinition
                {
                    Attribute = Key.PercentFreezeReduction,
                    Condition = Condition.OnlyWeapon,
                    Value = Formula.Constant(50),
                },
                new AuraDefinition
                {
                    Attribute = Key.PercentSlowReduction,
                    Condition = Condition.OnlyWeapon,
                    Value = Formula.Constant(50),
                },
            ],
        };
    }
}

