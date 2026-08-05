using BenchmarkDotNet.Running;

namespace TomasAI.IFM.Domain.MarketData.Feed.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
