namespace BazaarArena.Core;

/// <summary>能力触发类型常量，用于 AbilityDefinition.TriggerName，避免魔法字符串。</summary>
public static class Trigger
{
    public const string UseItem = "使用物品";
    public const string BattleStart = "战斗开始";
    /// <summary>任意物品施加冻结时触发（默认 Condition 为 SameSide，可重写为实现对方施加时触发）；每冻结一个目标计一次，由 PendingCount 控制 250ms 间隔。</summary>
    public const string Freeze = "触发冻结";
    /// <summary>任意物品施加减速时触发（默认 Condition 为 SameSide，可重写为实现对方施加时触发）；每减速一个目标计一次，由 PendingCount 控制 250ms 间隔。</summary>
    public const string Slow = "触发减速";
    /// <summary>任意物品施加加速时触发（默认 Condition 为 SameSide，可重写为实现对方施加时触发）；每加速一个目标计一次，由 PendingCount 控制 250ms 间隔。</summary>
    public const string Haste = "触发加速";
    /// <summary>任意物品造成暴击时触发（默认 Condition 为 SameSide，可重写为实现对方暴击时触发）；来源=暴击施放者。</summary>
    public const string Crit = "造成暴击";
    /// <summary>任意物品施加摧毁时触发，实现同 Slow：Condition 判定施加者，InvokeTargetCondition 判定被摧毁物品；须在将目标标记为 Destroyed 之前调用。</summary>
    public const string Destroy = "摧毁物品";
    /// <summary>任意物品施加灼烧时触发（默认 Condition 为 SameSide）；来源=施加灼烧的物品。</summary>
    public const string Burn = "触发灼烧";
    /// <summary>任意物品施加剧毒时触发（默认 Condition 为 SameSide）；来源=施加剧毒的物品。</summary>
    public const string Poison = "触发剧毒";
    /// <summary>己方获得护盾时触发（默认 Condition 为 SameSide）；来源=施加护盾的物品。</summary>
    public const string Shield = "获得护盾";
    /// <summary>弹药消耗时触发（某物品消耗 1 发弹药时，即 AmmoRemaining-- 后）；来源=消耗弹药的那个物品。默认 Condition 为 SameSide。可用 additionalCondition: Condition.AmmoDepleted 限定为「耗尽当次」。</summary>
    public const string Ammo = "弹药消耗";
}
