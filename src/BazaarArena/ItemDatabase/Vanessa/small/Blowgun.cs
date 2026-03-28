using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>吹箭枪（Blowgun）：海盗小型武器、遗物；▶ 造成伤害与剧毒（剧毒等量于伤害）。</summary>
public static class Blowgun
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "吹箭枪",
            Desc = "▶ 造成 {Damage} 伤害；▶ 造成剧毒，等量于此物品的伤害",
            Tags = Tag.Weapon | Tag.Relic,
            Cooldown = [8.0, 6.0],
            Damage = 2,
            Poison = 0,
            Abilities =
            [
                Ability.Damage,
                Ability.Poison,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Poison,
                    Value = Formula.Item(Key.Damage),
                },
            ],
        };
    }
}
