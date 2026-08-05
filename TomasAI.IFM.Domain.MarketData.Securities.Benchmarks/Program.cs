using BenchmarkDotNet.Running;

namespace TomasAI.IFM.Domain.MarketData.Securities.Benchmarks;

internal static class Program
{
    static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
