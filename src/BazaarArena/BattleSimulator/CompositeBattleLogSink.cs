namespace BazaarArena.BattleSimulator;

/// <summary>将日志转发到多个 Sink（如同时输出到控制台和文件）。</summary>
public class CompositeBattleLogSink : IBattleLogSink
{
    private readonly IBattleLogSink[] _sinks;

    public CompositeBattleLogSink(params IBattleLogSink[] sinks) => _sinks = sinks;

    public void OnFrameStart(int timeMs, int frame) { foreach (var s in _sinks) s.OnFrameStart(timeMs, frame); }
    public void OnHpSnapshot(int timeMs, int side0Hp, int side1Hp) { foreach (var s in _sinks) s.OnHpSnapshot(timeMs, side0Hp, side1Hp); }
    public void OnCast(ItemState caster, string itemName, int timeMs, int? ammoRemainingAfter = null) { foreach (var s in _sinks) s.OnCast(caster, itemName, timeMs, ammoRemainingAfter); }
    public void OnEffect(ItemState caster, string itemName, string effectKind, int value, int timeMs, bool isCrit = false, string? extraSuffix = null) { foreach (var s in _sinks) s.OnEffect(caster, itemName, effectKind, value, timeMs, isCrit, extraSuffix); }
    public void OnBurnTick(BattleSide victim, int burnDamage, int remainingBurn, int timeMs) { foreach (var s in _sinks) s.OnBurnTick(victim, burnDamage, remainingBurn, timeMs); }
    public void OnPoisonTick(BattleSide victim, int poisonDamage, int timeMs) { foreach (var s in _sinks) s.OnPoisonTick(victim, poisonDamage, timeMs); }
    public void OnRegenTick(BattleSide side, int heal, int timeMs) { foreach (var s in _sinks) s.OnRegenTick(side, heal, timeMs); }
    public void OnSandstormTick(int damage, int timeMs) { foreach (var s in _sinks) s.OnSandstormTick(damage, timeMs); }
    public void OnResult(int winnerSideIndex, int timeMs, bool isDraw) { foreach (var s in _sinks) s.OnResult(winnerSideIndex, timeMs, isDraw); }
}
