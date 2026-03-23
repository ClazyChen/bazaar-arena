using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace BazaarArena.Benchmarks;

/// <summary>
/// 主工程为 net10.0-windows（WPF），BDN 默认会生成 net10.0 子工程导致 NU1201；进程内发射工具链在同一进程内 JIT，避免该问题。
/// </summary>
internal sealed class BattleSimulatorBenchmarkConfig : ManualConfig
{
    public BattleSimulatorBenchmarkConfig()
    {
        // 避免命令行 --job 再并入 Default 工具链子工程（net10.0 与 net10.0-windows NU1201）
        UnionRule = ConfigUnionRule.AlwaysUseLocal;
        AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
        AddDiagnoser(MemoryDiagnoser.Default);
    }
}
