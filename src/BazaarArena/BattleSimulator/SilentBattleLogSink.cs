namespace BazaarArena.BattleSimulator;

/// <summary>不输出任何内容的日志 Sink，用于批量模拟。</summary>
public class SilentBattleLogSink : IBattleLogSink
{
    public void OnFrameStart(int timeMs, int frame) { }
    public void OnHpSnapshot(int timeMs, int side0Hp, int side1Hp) { }
    public void OnCast(int sideIndex, int itemIndex, string itemName, int timeMs) { }
    public void OnEffect(int sideIndex, int itemIndex, string itemName, string effectKind, int value, int timeMs, bool isCrit = false, string? extraSuffix = null) { }
    public void OnBurnTick(int sideIndex, int burnDamage, int remainingBurn, int timeMs) { }
    public void OnPoisonTick(int sideIndex, int poisonDamage, int timeMs) { }
    public void OnRegenTick(int sideIndex, int heal, int timeMs) { }
    public void OnSandstormTick(int damage, int timeMs) { }
    public void OnResult(int winnerSideIndex, int timeMs, bool isDraw) { }
}
