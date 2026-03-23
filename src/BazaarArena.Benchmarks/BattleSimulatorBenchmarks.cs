using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using ItemDb = BazaarArena.ItemDatabase.ItemDatabase;
using Sim = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena.Benchmarks;

/// <summary>单局 <see cref="Sim.Run"/> 吞吐与分配；Release 下运行。</summary>
[Config(typeof(BattleSimulatorBenchmarkConfig))]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class BattleSimulatorBenchmarks
{
    private Sim _sim = null!;
    private ItemDb _db = null!;
    private IItemTemplateResolver _resolver = null!;
    private readonly SilentBattleLogSink _sink = new();

    private Deck _tenRef1 = null!;
    private Deck _tenRef2 = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        BenchHarness.LoadLevelUps();
        _db = BenchHarness.CreateItemDatabase();
        _resolver = _db;
        _sim = new Sim();

        _tenRef1 = TenSlotDeckScenarios.ReferenceDeck1();
        _tenRef2 = TenSlotDeckScenarios.ReferenceDeck2();
    }

    /// <summary>10 槽参考卡组1 vs 参考卡组2（卡组1 为 9 条目含中型鲨齿爪占 2 槽）。</summary>
    [Benchmark(Baseline = true, Description = "10槽：参考卡组1 vs 参考卡组2")]
    public int Run_10Slot_Ref1_vs_Ref2() =>
        _sim.Run(_tenRef1, _tenRef2, _resolver, _sink, BattleLogLevel.None);
}
