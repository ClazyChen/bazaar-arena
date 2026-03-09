namespace BazaarArena.BattleSimulator;

/// <summary>将日志转发到多个 Sink（如同时输出到控制台和文件）。</summary>
public class CompositeBattleLogSink : IBattleLogSink
{
    private readonly IBattleLogSink[] _sinks;

    public CompositeBattleLogSink(params IBattleLogSink[] sinks) => _sinks = sinks;

    public void OnFrameStart(int timeMs, int frame) { foreach (var s in _sinks) s.OnFrameStart(timeMs, frame); }
    public void OnHpSnapshot(int timeMs, int side0Hp, int side1Hp) { foreach (var s in _sinks) s.OnHpSnapshot(timeMs, side0Hp, side1Hp); }
    public void OnCast(int sideIndex, int itemIndex, string itemName, int timeMs) { foreach (var s in _sinks) s.OnCast(sideIndex, itemIndex, itemName, timeMs); }
    public void OnEffect(int sideIndex, int itemIndex, string itemName, string effectKind, int value, int timeMs, bool isCrit = false) { foreach (var s in _sinks) s.OnEffect(sideIndex, itemIndex, itemName, effectKind, value, timeMs, isCrit); }
    public void OnBurnTick(int sideIndex, int burnDamage, int remainingBurn, int timeMs) { foreach (var s in _sinks) s.OnBurnTick(sideIndex, burnDamage, remainingBurn, timeMs); }
    public void OnPoisonTick(int sideIndex, int poisonDamage, int timeMs) { foreach (var s in _sinks) s.OnPoisonTick(sideIndex, poisonDamage, timeMs); }
    public void OnRegenTick(int sideIndex, int heal, int timeMs) { foreach (var s in _sinks) s.OnRegenTick(sideIndex, heal, timeMs); }
    public void OnSandstormTick(int damage, int timeMs) { foreach (var s in _sinks) s.OnSandstormTick(damage, timeMs); }
    public void OnResult(int winnerSideIndex, int timeMs, bool isDraw) { foreach (var s in _sinks) s.OnResult(winnerSideIndex, timeMs, isDraw); }
}
