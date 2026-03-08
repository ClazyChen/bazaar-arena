namespace BazaarArena.BattleSimulator;

/// <summary>将战斗日志按行追加到指定委托（可由 UI 线程接收并写入 TextBox）。</summary>
public class TextBoxBattleLogSink : IBattleLogSink
{
    private readonly BattleLogLevel _level;
    private readonly Action<string> _appendLine;

    public TextBoxBattleLogSink(BattleLogLevel level, Action<string> appendLine)
    {
        _level = level;
        _appendLine = appendLine;
    }

    public void OnFrameStart(int timeMs, int frame)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _appendLine($"[帧 {frame}] 时间 {timeMs}ms");
    }

    public void OnCast(int sideIndex, string itemName, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _appendLine($"  玩家{sideIndex + 1} 施放 [{itemName}] @ {timeMs}ms");
    }

    public void OnEffect(int sideIndex, string itemName, string effectKind, int value, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _appendLine($"  玩家{sideIndex + 1} [{itemName}] {effectKind} {value} @ {timeMs}ms");
    }

    public void OnBurnTick(int sideIndex, int burnDamage, int remainingBurn, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _appendLine($"  灼烧结算 玩家{sideIndex + 1} 受到{burnDamage} 剩余灼烧{remainingBurn} @ {timeMs}ms");
    }

    public void OnPoisonTick(int sideIndex, int poisonDamage, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _appendLine($"  剧毒结算 玩家{sideIndex + 1} 受到{poisonDamage} @ {timeMs}ms");
    }

    public void OnRegenTick(int sideIndex, int heal, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _appendLine($"  生命再生 玩家{sideIndex + 1} 回复{heal} @ {timeMs}ms");
    }

    public void OnSandstormTick(int damage, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _appendLine($"  沙尘暴 双方受到{damage} @ {timeMs}ms");
    }

    public void OnResult(int winnerSideIndex, int timeMs, bool isDraw)
    {
        if (_level == BattleLogLevel.None) return;
        _appendLine(isDraw ? $"[结果] 平局 @ {timeMs}ms" : $"[结果] 玩家{winnerSideIndex + 1} 获胜 @ {timeMs}ms");
    }
}
