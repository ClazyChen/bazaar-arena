using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

/// <summary>船骸（Shipwreck）：海盗大型水系、载具、地产、遗物；己方水系物品多重释放 +1。</summary>
public static class Shipwreck
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "船骸",
            Desc = "水系物品 +{Custom_0} 多重释放",
            Tags = Tag.Aquatic | Tag.Vehicle | Tag.Property | Tag.Relic,
            Cooldown = 0.0,
            Custom_0 = 1,
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Condition = Condition.SameSide & Condition.WithTag(Tag.Aquatic),
                    Value = Formula.Caster(Key.Custom_0),
                },
            ],
        };
    }
}
