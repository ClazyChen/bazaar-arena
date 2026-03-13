using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>战斗中单件物品的运行时状态。所有 int/bool 运行时变量均存于 Template 字典，可通过名字统一读取。</summary>
public class BattleItemState
{
    /// <summary>本物品在本次战斗中的阵营下标（0 或 1），由 Run 初始化时设置；读写 Template 字典。</summary>
    public int SideIndex { get => Template.GetInt(ItemTemplate.KeySideIndex, 0); set => Template.SetInt(ItemTemplate.KeySideIndex, value); }
    /// <summary>本物品在己方物品列表中的下标，由 Run 初始化时设置；读写 Template 字典。</summary>
    public int ItemIndex { get => Template.GetInt(ItemTemplate.KeyItemIndex, 0); set => Template.SetInt(ItemTemplate.KeyItemIndex, value); }

    public ItemTemplate Template { get; set; }
    /// <summary>物品等级；读写 Template 字典（KeyTier 存为 int）。</summary>
    public ItemTier Tier { get => (ItemTier)Template.GetInt(ItemTemplate.KeyTier, (int)ItemTier.Bronze); set => Template.SetInt(ItemTemplate.KeyTier, (int)value); }

    /// <summary>已过的冷却时间（毫秒）。</summary>
    public int CooldownElapsedMs { get => Template.GetInt(ItemTemplate.KeyCooldownElapsedMs, 0); set => Template.SetInt(ItemTemplate.KeyCooldownElapsedMs, value); }
    public int HasteRemainingMs { get => Template.GetInt(ItemTemplate.KeyHasteRemainingMs, 0); set => Template.SetInt(ItemTemplate.KeyHasteRemainingMs, value); }
    public int SlowRemainingMs { get => Template.GetInt(ItemTemplate.KeySlowRemainingMs, 0); set => Template.SetInt(ItemTemplate.KeySlowRemainingMs, value); }
    public int FreezeRemainingMs { get => Template.GetInt(ItemTemplate.KeyFreezeRemainingMs, 0); set => Template.SetInt(ItemTemplate.KeyFreezeRemainingMs, value); }
    /// <summary>是否处于飞行状态；战斗开始为 false，「开始飞行」/「结束飞行」效果可修改。</summary>
    public bool InFlight { get => Template.GetBool(ItemTemplate.KeyInFlight); set => Template.SetBool(ItemTemplate.KeyInFlight, value); }
    public bool Destroyed { get => Template.GetBool(ItemTemplate.KeyDestroyed); set => Template.SetBool(ItemTemplate.KeyDestroyed, value); }
    public int AmmoRemaining { get => Template.GetInt(ItemTemplate.KeyAmmoRemaining, 0); set => Template.SetInt(ItemTemplate.KeyAmmoRemaining, value); }

    /// <summary>指定能力上次触发的时间（毫秒），用于 250ms 间隔；存于 Template 键 LastTriggerMs_{abilityIndex}。</summary>
    public int GetLastTriggerMs(int abilityIndex) => Template.GetInt(ItemTemplate.KeyLastTriggerMsPrefix + abilityIndex, -1000);
    /// <summary>设置指定能力上次触发的时间（毫秒）。</summary>
    public void SetLastTriggerMs(int abilityIndex, int timeMs) => Template.SetInt(ItemTemplate.KeyLastTriggerMsPrefix + abilityIndex, timeMs);

    /// <summary>本次暴击判定所适用的时间（毫秒）；与当前帧 timeMs 相同时表示本帧已判定可复用。</summary>
    public int CritTimeMs { get => Template.GetInt(ItemTemplate.KeyCritTimeMs, -1); set => Template.SetInt(ItemTemplate.KeyCritTimeMs, value); }
    /// <summary>本次判定是否暴击；仅当 CritTimeMs == 当前帧 timeMs 时有效。</summary>
    public bool IsCritThisUse { get => Template.GetBool(ItemTemplate.KeyIsCritThisUse); set => Template.SetBool(ItemTemplate.KeyIsCritThisUse, value); }
    /// <summary>本次判定若暴击时的暴击伤害百分比。</summary>
    public int CritDamagePercentThisUse { get => Template.GetInt(ItemTemplate.KeyCritDamagePercentThisUse, 200); set => Template.SetInt(ItemTemplate.KeyCritDamagePercentThisUse, value); }

    public BattleItemState(ItemTemplate template, ItemTier tier)
    {
        Template = template;
        template.SetInt(ItemTemplate.KeyTier, (int)tier);
        template.SetInt(ItemTemplate.KeyAmmoRemaining, template.GetInt(Key.AmmoCap, tier));
        for (int i = 0; i < template.Abilities.Count; i++)
            template.SetInt(ItemTemplate.KeyLastTriggerMsPrefix + i, -1000);
    }
}
