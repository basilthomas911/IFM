using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using TomasAI.IFM.Framework.Caching.Benchmarks;

if (args.Contains("--verify-store-paths", StringComparer.Ordinal))
{
    await using var fixture = new RedisRemovalFixture();
    await fixture.StartAsync();
    await fixture.MeasureStorePathsAsync();
    return;
}
if (args.Contains("--verify-redis-removal", StringComparer.Ordinal))
{
    await using var fixture = new RedisRemovalFixture();
    await fixture.StartAsync();
    await fixture.MeasureColdAndConcurrentAsync();
    return;
}
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args,
    ManualConfig.Create(DefaultConfig.Instance).WithBuildTimeout(TimeSpan.FromMinutes(10)));
