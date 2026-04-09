using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

/// <summary>潜行滑翔机（Stealth Glider）：海盗大型载具、科技；战斗开始无敌，首次使用任意物品（含自身）解除无敌。</summary>
public static class StealthGlider
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "潜行滑翔机",
            Desc = "战斗开始时，你获得无敌 {InvincibleSeconds} 秒；首次使用物品时，解除无敌",
            Tags = Tag.Vehicle | Tag.Tech,
            Cooldown = 0.0,
            Invincible = [8.0, 10.0],
            Abilities =
            [
                Ability.Invincible.Override(
                    trigger: Trigger.BattleStart,
                    priority: AbilityPriority.Medium),
                Ability.ClearInvincible.Override(
                    trigger: Trigger.UseOtherItem,
                    priority: AbilityPriority.Immediate),
            ],
        };
    }
}
