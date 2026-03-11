namespace BazaarArena.BattleSimulator;

/// <summary>将战斗日志输出到控制台，根据级别决定是否输出。</summary>
public class ConsoleBattleLogSink : IBattleLogSink
{
    private readonly BattleLogLevel _level;

    public ConsoleBattleLogSink(BattleLogLevel level) => _level = level;

    public void OnFrameStart(int timeMs, int frame)
    {
        if (_level != BattleLogLevel.Detailed) return;
        Console.WriteLine($"[帧 {frame}] 时间 {timeMs}ms");
    }

    public void OnHpSnapshot(int timeMs, int side0Hp, int side1Hp) { }

    public void OnCast(BattleItemState caster, string itemName, int timeMs, int? ammoRemainingAfter = null)
    {
        if (_level != BattleLogLevel.Detailed) return;
        string suffix = ammoRemainingAfter.HasValue ? $" 剩余弹药 {ammoRemainingAfter.Value}" : "";
        Console.WriteLine($"  玩家{caster.SideIndex + 1} 施放 [{itemName}] @ {timeMs}ms{suffix}");
    }

    public void OnEffect(BattleItemState caster, string itemName, string effectKind, int value, int timeMs, bool isCrit = false, string? extraSuffix = null)
    {
        if (_level != BattleLogLevel.Detailed) return;
        string critSuffix = isCrit ? " （暴击）" : "";
        string valueStr = EffectLogFormat.FormatEffectValue(effectKind, value);
        string valuePart = string.IsNullOrEmpty(valueStr) ? "" : " " + valueStr;
        Console.WriteLine($"  玩家{caster.SideIndex + 1} [{itemName}] {effectKind}{valuePart}{extraSuffix}{critSuffix} @ {timeMs}ms");
    }

    public void OnBurnTick(BattleSide victim, int burnDamage, int remainingBurn, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        Console.WriteLine($"  灼烧结算 玩家{victim.SideIndex + 1} 受到{burnDamage} 剩余灼烧{remainingBurn} @ {timeMs}ms");
    }

    public void OnPoisonTick(BattleSide victim, int poisonDamage, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        Console.WriteLine($"  剧毒结算 玩家{victim.SideIndex + 1} 受到{poisonDamage} @ {timeMs}ms");
    }

    public void OnRegenTick(BattleSide side, int heal, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        Console.WriteLine($"  生命再生 玩家{side.SideIndex + 1} 回复{heal} @ {timeMs}ms");
    }

    public void OnSandstormTick(int damage, int timeMs)
    {
        if (_level != BattleLogLevel.Detailed) return;
        Console.WriteLine($"  沙尘暴 双方受到{damage} @ {timeMs}ms");
    }

    public void OnResult(int winnerSideIndex, int timeMs, bool isDraw)
    {
        if (_level == BattleLogLevel.None) return;
        if (isDraw)
            Console.WriteLine($"[结果] 平局 @ {timeMs}ms");
        else
            Console.WriteLine($"[结果] 玩家{winnerSideIndex + 1} 获胜 @ {timeMs}ms");
    }
}
