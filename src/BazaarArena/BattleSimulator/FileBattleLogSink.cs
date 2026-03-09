using System.IO;

namespace BazaarArena.BattleSimulator;

/// <summary>将战斗日志写入文件，文件名使用当前时间戳，由 <see cref="LogPaths.GetTimestampedLogPath"/> 生成。</summary>
public class FileBattleLogSink : IBattleLogSink, IDisposable
{
    private readonly BattleLogLevel _level;
    private readonly StreamWriter _writer;
    private readonly string _path;

    public FileBattleLogSink(BattleLogLevel level, string? filePath = null)
    {
        _level = level;
        _path = filePath ?? LogPaths.GetTimestampedLogPath();
        _writer = new StreamWriter(_path, append: false) { AutoFlush = true };
    }

    /// <summary>当前日志文件完整路径。</summary>
    public string Path => _path;

    public void OnFrameStart(int timeMs, int frame)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _writer.WriteLine($"[帧 {frame}] 时间 {timeMs}ms");
    }

    public void OnHpSnapshot(int timeMs, int side0Hp, int side1Hp) { }

    public void OnCast(int sideIndex, int itemIndex, string itemName, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _writer.WriteLine($"  玩家{sideIndex + 1} 施放 [{itemName}] @ {timeMs}ms");
    }

    public void OnEffect(int sideIndex, int itemIndex, string itemName, string effectKind, int value, int timeMs, bool isCrit = false)
    {
        if (_level != BattleLogLevel.Detailed) return;
        string critSuffix = isCrit ? " （暴击）" : "";
        string valueStr = EffectLogFormat.FormatEffectValue(effectKind, value);
        _writer.WriteLine($"  玩家{sideIndex + 1} [{itemName}] {effectKind} {valueStr}{critSuffix} @ {timeMs}ms");
    }

    public void OnBurnTick(int sideIndex, int burnDamage, int remainingBurn, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _writer.WriteLine($"  灼烧结算 玩家{sideIndex + 1} 受到{burnDamage} 剩余灼烧{remainingBurn} @ {timeMs}ms");
    }

    public void OnPoisonTick(int sideIndex, int poisonDamage, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _writer.WriteLine($"  剧毒结算 玩家{sideIndex + 1} 受到{poisonDamage} @ {timeMs}ms");
    }

    public void OnRegenTick(int sideIndex, int heal, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _writer.WriteLine($"  生命再生 玩家{sideIndex + 1} 回复{heal} @ {timeMs}ms");
    }

    public void OnSandstormTick(int damage, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _writer.WriteLine($"  沙尘暴 双方受到{damage} @ {timeMs}ms");
    }

    public void OnResult(int winnerSideIndex, int timeMs, bool isDraw)
    {
        if (_level == BattleLogLevel.None) return;
        if (isDraw)
            _writer.WriteLine($"[结果] 平局 @ {timeMs}ms");
        else
            _writer.WriteLine($"[结果] 玩家{winnerSideIndex + 1} 获胜 @ {timeMs}ms");
    }

    public void Dispose() => _writer.Dispose();
}
