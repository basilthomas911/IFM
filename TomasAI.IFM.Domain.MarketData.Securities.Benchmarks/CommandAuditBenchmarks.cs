using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TomasAI.IFM.Domain.MarketData.Securities.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8, invocationCount: 1)]
public class CommandAuditBenchmarks
{
    Task _pendingAudit = Task.CompletedTask;

    [Benchmark(Baseline = true)]
    public bool BeforeBlockingParse()
    {
        _pendingAudit = PersistAuditAsync();
        _pendingAudit.GetAwaiter().GetResult();
        return true;
    }

    [Benchmark]
    public bool AfterNonBlockingParse()
    {
        _pendingAudit = PersistAuditAsync();
        return _pendingAudit.IsCompleted;
    }

    static async Task PersistAuditAsync()
        => await Task.Delay(1).ConfigureAwait(false);
}
