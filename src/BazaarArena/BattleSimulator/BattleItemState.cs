using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>战斗中单件物品的运行时状态。</summary>
public class BattleItemState
{
    public ItemTemplate Template { get; set; }
    public ItemTier Tier { get; set; }

    /// <summary>已过的冷却时间（毫秒）。</summary>
    public int CooldownElapsedMs { get; set; }

    public int HasteRemainingMs { get; set; }
    public int SlowRemainingMs { get; set; }
    public int FreezeRemainingMs { get; set; }
    public bool Destroyed { get; set; }
    public int AmmoRemaining { get; set; }

    /// <summary>每个能力上次触发的时间（毫秒），用于 250ms 间隔。</summary>
    public List<int> LastTriggerMsByAbility { get; set; } = [];

    public BattleItemState(ItemTemplate template, ItemTier tier)
    {
        Template = template;
        Tier = tier;
        AmmoRemaining = template.GetInt("AmmoCap", tier);
        for (int i = 0; i < template.Abilities.Count; i++)
            LastTriggerMsByAbility.Add(-1000);
    }

    public int GetCooldownMs() => Template.GetInt("CooldownMs", Tier);
    public int GetCritRatePercent() => Template.GetInt("CritRatePercent", Tier);
    public int GetMulticast() => Template.GetInt("Multicast", Tier, 1);
    public int GetAmmoCap() => Template.GetInt("AmmoCap", Tier);
}
