namespace BazaarArena.BattleSimulator;

/// <summary>战斗日志输出，由模拟器在关键步骤调用。</summary>
public interface IBattleLogSink
{
    void OnFrameStart(int timeMs, int frame);
    /// <summary>当前帧双方当前生命值快照，用于强度曲线纵轴（当前生命值）。</summary>
    void OnHpSnapshot(int timeMs, int side0Hp, int side1Hp);
    void OnCast(int sideIndex, int itemIndex, string itemName, int timeMs);
    /// <param name="extraSuffix">可选后缀，如冻结时为 " →[物品名] →[物品名]"。</param>
    void OnEffect(int sideIndex, int itemIndex, string itemName, string effectKind, int value, int timeMs, bool isCrit = false, string? extraSuffix = null);
    void OnBurnTick(int sideIndex, int burnDamage, int remainingBurn, int timeMs);
    void OnPoisonTick(int sideIndex, int poisonDamage, int timeMs);
    void OnRegenTick(int sideIndex, int heal, int timeMs);
    void OnSandstormTick(int damage, int timeMs);
    void OnResult(int winnerSideIndex, int timeMs, bool isDraw);
}
