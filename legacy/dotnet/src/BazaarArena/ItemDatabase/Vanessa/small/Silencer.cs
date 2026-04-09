using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>消音器（Silencer）：海盗小型科技；左侧武器伤害提高；若只有 1 件武器则其冷却缩短。</summary>
public static class Silencer
{
    /// <summary>消音器（版本 7，银）：无冷却 小 银 科技；此物品左侧的武器伤害 +25 » +50 » +75；如果只有 1 件武器，则其冷却时间缩短 5 » 10 » 15%。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "消音器",
            Desc = "此物品左侧的武器 {+Custom_0} 伤害；如果只有 1 件武器，则其冷却时间缩短 {Custom_1%}",
            Tags = Tag.Tech,
            Cooldown = 0.0,
            Custom_0 = [25, 50, 75],
            Custom_1 = [5, 10, 15],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Damage,
                    Condition = Condition.LeftOfCaster & Condition.WithTag(Tag.Weapon),
                    Value = Formula.Caster(Key.Custom_0),
                },
                new AuraDefinition
                {
                    Attribute = Key.PercentCooldownReduction,
                    Condition = Condition.OnlyWeapon & Condition.HasCooldown,
                    Value = Formula.Caster(Key.Custom_1),
                },
            ],
        };
    }
}

