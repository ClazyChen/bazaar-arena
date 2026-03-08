namespace BazaarArena.BattleSimulator;

/// <summary>战斗日志输出，由模拟器在关键步骤调用。</summary>
public interface IBattleLogSink
{
    void OnFrameStart(int timeMs, int frame);
    void OnCast(int sideIndex, string itemName, int timeMs);
    void OnEffect(int sideIndex, string itemName, string effectKind, int value, int timeMs);
    void OnBurnTick(int sideIndex, int burnDamage, int remainingBurn, int timeMs);
    void OnPoisonTick(int sideIndex, int poisonDamage, int timeMs);
    void OnRegenTick(int sideIndex, int heal, int timeMs);
    void OnSandstormTick(int damage, int timeMs);
    void OnResult(int winnerSideIndex, int timeMs, bool isDraw);
}
