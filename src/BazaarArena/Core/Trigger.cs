namespace BazaarArena.Core;

/// <summary>能力触发类型常量（独立 int 编号区，不与 Key 复用）。</summary>
public static class Trigger
{
    public const int UseItem = 100;
    public const int BattleStart = 101;
    /// <summary>任意物品施加冻结时触发（默认 Condition 为 SameSide，可重写为实现对方施加时触发）；每冻结一个目标计一次，由 PendingCount 控制 250ms 间隔。</summary>
    public const int Freeze = 102;
    /// <summary>任意物品施加减速时触发（默认 Condition 为 SameSide，可重写为实现对方施加时触发）；每减速一个目标计一次，由 PendingCount 控制 250ms 间隔。</summary>
    public const int Slow = 103;
    /// <summary>任意物品施加加速时触发（默认 Condition 为 SameSide，可重写为实现对方施加时触发）；每加速一个目标计一次，由 PendingCount 控制 250ms 间隔。</summary>
    public const int Haste = 104;
    /// <summary>任意物品造成暴击时触发（默认 Condition 为 SameSide，可重写为实现对方暴击时触发）；来源=暴击施放者。</summary>
    public const int Crit = 105;
    /// <summary>任意物品施加摧毁时触发，实现同 Slow：Condition 判定施加者，InvokeTargetCondition 判定被摧毁物品；须在将目标标记为 Destroyed 之前调用。</summary>
    public const int Destroy = 106;
    /// <summary>任意物品施加灼烧时触发（默认 Condition 为 SameSide）；来源=施加灼烧的物品。</summary>
    public const int Burn = 107;
    /// <summary>任意物品施加剧毒时触发（默认 Condition 为 SameSide）；来源=施加剧毒的物品。</summary>
    public const int Poison = 108;
    /// <summary>己方获得护盾时触发（默认 Condition 为 SameSide）；来源=施加护盾的物品。</summary>
    public const int Shield = 109;
    /// <summary>弹药消耗时触发（某物品消耗 1 发弹药时，即 AmmoRemaining-- 后）；来源=消耗弹药的那个物品。默认 Condition 为 SameSide。可用 additionalCondition: Condition.AmmoDepleted 限定为「耗尽当次」。</summary>
    public const int Ammo = 110;
    /// <summary>即将落败时触发（该方 Hp≤0 时，每场战斗每方最多触发一次，由模拟器步骤 10 用阵营标记保证「首次」）。默认 Condition 为 SameSide。</summary>
    public const int AboutToLose = 111;
}
