using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>吸血章鱼（Vampire Squid）：海盗小型武器、水系、伙伴；▶ 造成伤害；此物品伤害提高等同于其暴击率；吸血。</summary>
public static class VampireSquid
{
    /// <summary>吸血章鱼（版本 10，银）：5s 小 银 武器 水系 伙伴；▶ 造成 15 » 30 » 45 伤害；此物品 +伤害，等同于其暴击率；吸血。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "吸血章鱼",
            Desc = "▶ 造成 {Damage} 伤害；此物品 +伤害，等同于其暴击率；吸血",
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Friend,
            Cooldown = 5.0,
            Damage = [15, 30, 45],
            LifeSteal = 1,
            Abilities =
            [
                Ability.Damage,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Damage,
                    Value = Formula.Caster(Key.CritRate),
                },
            ],
        };
    }
}

