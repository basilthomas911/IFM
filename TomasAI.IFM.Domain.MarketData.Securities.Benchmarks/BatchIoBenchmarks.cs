using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TomasAI.IFM.Domain.MarketData.Securities.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8, invocationCount: 1)]
public class BatchIoBenchmarks
{
    const int Concurrency = 8;
    string[] _contractIds = [];

    [Params(8, 32)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
        => _contractIds = Enumerable.Range(0, Count)
            .Select(static index => $"ES-OPTION-{index:D5}")
            .ToArray();

    [Benchmark(Baseline = true)]
    public async Task<int> BeforeSequentialIdReads()
    {
        var found = 0;
        for (var index = 0; index < _contractIds.Length; index++)
        {
            await SimulatedIoAsync().ConfigureAwait(false);
            if ((index & 1) == 0)
                found++;
        }
        return found;
    }

    [Benchmark]
    public async Task<int> AfterBulkIdRead()
    {
        await SimulatedIoAsync().ConfigureAwait(false);
        var found = 0;
        for (var index = 0; index < _contractIds.Length; index++)
            if ((index & 1) == 0)
                found++;
        return found;
    }

    [Benchmark]
    public async Task<int> BeforeSerialEnrichmentAndWrites()
    {
        var completed = 0;
        for (var index = 0; index < _contractIds.Length; index++)
        {
            await SimulatedIoAsync().ConfigureAwait(false);
            await SimulatedIoAsync().ConfigureAwait(false);
            completed++;
        }
        return completed;
    }

    [Benchmark]
    public async Task<int> AfterBoundedEnrichmentAndBatchWrite()
    {
        var completed = 0;
        for (var offset = 0; offset < _contractIds.Length; offset += Concurrency)
        {
            var count = Math.Min(Concurrency, _contractIds.Length - offset);
            var tasks = new Task[count];
            for (var index = 0; index < tasks.Length; index++)
                tasks[index] = SimulatedIoAsync();
            await Task.WhenAll(tasks).ConfigureAwait(false);
            completed += count;
        }
        await SimulatedIoAsync().ConfigureAwait(false);
        return completed;
    }

    static async Task SimulatedIoAsync()
        => await Task.Delay(1).ConfigureAwait(false);
}
