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
        // GUI 单次模拟只保留能力相关日志，不输出每帧行
        return;
    }

    public void OnHpSnapshot(int timeMs, int side0Hp, int side1Hp) { }

    private static string TimeSec(int timeMs) => (timeMs / 1000.0).ToString("F2") + "s";

    public void OnCast(BattleItemState caster, string itemName, int timeMs, int? ammoRemainingAfter = null)
    {
        if (!ammoRemainingAfter.HasValue) return; // 无弹药时不单独输出施放行
        _appendLine($"[{TimeSec(timeMs)}] 玩家{caster.SideIndex + 1} 施放 [{itemName}] 剩余弹药 {ammoRemainingAfter.Value}");
    }

    public void OnEffect(BattleItemState caster, string itemName, string effectKind, int value, int timeMs, bool isCrit = false, string? extraSuffix = null)
    {
        if (_level != BattleLogLevel.Detailed) return;
        string critSuffix = isCrit ? " （暴击）" : "";
        string valueStr = EffectLogFormat.FormatEffectValue(effectKind, value);
        string valuePart = string.IsNullOrEmpty(valueStr) ? "" : " " + valueStr;
        _appendLine($"[{TimeSec(timeMs)}] 玩家{caster.SideIndex + 1} [{itemName}] {effectKind}{valuePart}{extraSuffix}{critSuffix}");
    }

    public void OnBurnTick(BattleSide victim, int burnDamage, int remainingBurn, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _appendLine($"[{TimeSec(timeMs)}] 灼烧结算 玩家{victim.SideIndex + 1} 受到{burnDamage} 剩余灼烧{remainingBurn}");
    }

    public void OnPoisonTick(BattleSide victim, int poisonDamage, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _appendLine($"[{TimeSec(timeMs)}] 剧毒结算 玩家{victim.SideIndex + 1} 受到{poisonDamage}");
    }

    public void OnRegenTick(BattleSide side, int heal, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _appendLine($"[{TimeSec(timeMs)}] 生命再生 玩家{side.SideIndex + 1} 回复{heal}");
    }

    public void OnSandstormTick(int damage, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        _appendLine($"[{TimeSec(timeMs)}] 沙尘暴 双方受到{damage}");
    }

    public void OnResult(int winnerSideIndex, int timeMs, bool isDraw)
    {
        if (_level == BattleLogLevel.None) return;
        _appendLine(isDraw ? $"[{TimeSec(timeMs)}] [结果] 平局" : $"[{TimeSec(timeMs)}] [结果] 玩家{winnerSideIndex + 1} 获胜");
    }
}
