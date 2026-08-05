using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

namespace TomasAI.IFM.Domain.SystemAdmin.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class CommandAuditBenchmarks
{
    Task _lastAudit = Task.CompletedTask;

    [Benchmark(Baseline = true)]
    public void BeforeBlockingParse()
        => SimulatedAuditAsync().GetAwaiter().GetResult();

    [Benchmark]
    public void AfterTrackedParseRelease()
    {
        var audit = SimulatedAuditAsync();
        if (audit.IsCompleted)
            audit.GetAwaiter().GetResult();
        _lastAudit = audit;
    }

    [GlobalCleanup]
    public void Cleanup() => _lastAudit.GetAwaiter().GetResult();

    static Task SimulatedAuditAsync() => Task.Delay(1);
}
