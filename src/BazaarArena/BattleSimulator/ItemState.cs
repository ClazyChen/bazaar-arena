using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>
/// 战斗中单件物品的运行时状态（字段直存版本）。
/// 设计目标：替代 BattleItemState 的 Key 字典读写路径，减少热路径间接访问。
/// </summary>
public class ItemState
{
    /// <summary>静态模板定义（只读语义，运行时状态不再回写模板 Key）。</summary>
    public ItemTemplate Template;

    /// <summary>阵营与槽位索引。</summary>
    public int SideIndex;
    public int ItemIndex;

    /// <summary>物品档位。</summary>
    public ItemTier Tier;

    /// <summary>冷却与状态字段（毫秒）。</summary>
    public int CooldownElapsedMs;
    public int HasteRemainingMs;
    public int SlowRemainingMs;
    public int FreezeRemainingMs;

    /// <summary>战斗状态字段。</summary>
    public bool InFlight;
    public bool Destroyed;
    public int AmmoRemaining;

    /// <summary>
    /// 每个能力上次触发时间（毫秒），替代 LastTriggerMs_{index} 动态 Key。
    /// </summary>
    public int[] LastTriggerMsByAbility;

    /// <summary>按帧缓存暴击判定，避免同帧重复掷骰。</summary>
    public int CritTimeMs;
    public bool IsCritThisUse;
    public int CritDamagePercentThisUse;

    public ItemState(ItemTemplate template, ItemTier tier)
    {
        Template = template;
        Tier = tier;

        SideIndex = 0;
        ItemIndex = 0;

        CooldownElapsedMs = 0;
        HasteRemainingMs = 0;
        SlowRemainingMs = 0;
        FreezeRemainingMs = 0;

        InFlight = false;
        Destroyed = false;
        AmmoRemaining = template.GetInt(Key.AmmoCap, tier);

        LastTriggerMsByAbility = new int[template.Abilities.Count];
        for (int i = 0; i < LastTriggerMsByAbility.Length; i++)
            LastTriggerMsByAbility[i] = -1000;

        CritTimeMs = -1;
        IsCritThisUse = false;
        CritDamagePercentThisUse = 200;
    }

    public int GetLastTriggerMs(int abilityIndex)
    {
        if ((uint)abilityIndex >= (uint)LastTriggerMsByAbility.Length)
            return -1000;
        return LastTriggerMsByAbility[abilityIndex];
    }

    public void SetLastTriggerMs(int abilityIndex, int timeMs)
    {
        if ((uint)abilityIndex >= (uint)LastTriggerMsByAbility.Length)
            return;
        LastTriggerMsByAbility[abilityIndex] = timeMs;
    }
}

