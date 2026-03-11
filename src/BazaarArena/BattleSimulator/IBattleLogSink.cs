namespace BazaarArena.BattleSimulator;

/// <summary>战斗日志输出，由模拟器在关键步骤调用。</summary>
public interface IBattleLogSink
{
    void OnFrameStart(int timeMs, int frame);
    /// <summary>当前帧双方当前生命值快照，用于强度曲线纵轴（当前生命值）。</summary>
    void OnHpSnapshot(int timeMs, int side0Hp, int side1Hp);
    /// <param name="caster">施放物品。</param>
    /// <param name="ammoRemainingAfter">若不为 null，表示该物品有弹药且使用后剩余弹药数（用于日志显示）。</param>
    void OnCast(BattleItemState caster, string itemName, int timeMs, int? ammoRemainingAfter = null);
    /// <param name="caster">施放物品。</param>
    /// <param name="extraSuffix">可选后缀，如冻结时为 " →[物品名] →[物品名]"。</param>
    void OnEffect(BattleItemState caster, string itemName, string effectKind, int value, int timeMs, bool isCrit = false, string? extraSuffix = null);
    void OnBurnTick(BattleSide victim, int burnDamage, int remainingBurn, int timeMs);
    void OnPoisonTick(BattleSide victim, int poisonDamage, int timeMs);
    void OnRegenTick(BattleSide side, int heal, int timeMs);
    void OnSandstormTick(int damage, int timeMs);
    void OnResult(int winnerSideIndex, int timeMs, bool isDraw);
}
