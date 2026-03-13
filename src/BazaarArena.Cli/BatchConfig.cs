using System.Collections.Generic;

namespace BazaarArena.Cli;

/// <summary>批量配置中的单次对战项。</summary>
internal sealed class BatchBattleConfig
{
    public string Deck1 { get; set; } = "";
    public string Deck2 { get; set; } = "";
    public string Log { get; set; } = "";
}

internal sealed class BatchConfig
{
    public List<BatchBattleConfig> Battles { get; set; } = new();
}

