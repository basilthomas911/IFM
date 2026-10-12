using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.FirstOrDefault() == "--plan-business-load")
            await PostgresPlanBusinessLoadRunner.RunAsync(args.Skip(1).ToArray());
        else if (args.FirstOrDefault() == "--plan-business")
            BenchmarkRunner.Run<PostgresPlanBusinessBenchmarks>(config: ManualConfig.Create(DefaultConfig.Instance).WithBuildTimeout(TimeSpan.FromMinutes(10)), args: args.Skip(1).ToArray());
        else
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
