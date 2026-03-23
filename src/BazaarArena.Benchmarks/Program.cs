using BenchmarkDotNet.Running;

namespace BazaarArena.Benchmarks;

internal static class Program
{
    private static int Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }
}
