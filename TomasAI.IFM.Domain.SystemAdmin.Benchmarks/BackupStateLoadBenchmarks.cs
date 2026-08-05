using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.SystemAdmin.Command.State;

namespace TomasAI.IFM.Domain.SystemAdmin.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class BackupStateLoadBenchmarks
{
    [Benchmark(Baseline = true)]
    public async Task<SystemAdminCommandState> BeforeSnapshotRoundTrip()
    {
        await Task.Delay(1).ConfigureAwait(false);
        return new SystemAdminCommandState();
    }

    [Benchmark]
    public SystemAdminCommandState AfterFreshState()
        => new();
}
