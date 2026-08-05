using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TomasAI.IFM.Domain.MarketData.Feed.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class AsyncFanOutBenchmarks
{
    [Params(3, 5)]
    public int OperationCount { get; set; }

    [Benchmark(Baseline = true)]
    public async Task<int> BeforeSequential()
    {
        var total = 0;
        for (var index = 0; index < OperationCount; index++)
            total += await SimulatedIoAsync(index);
        return total;
    }

    [Benchmark]
    public async Task<int> AfterConcurrent()
    {
        var operations = new Task<int>[OperationCount];
        for (var index = 0; index < operations.Length; index++)
            operations[index] = SimulatedIoAsync(index);
        var results = await Task.WhenAll(operations);
        var total = 0;
        for (var index = 0; index < results.Length; index++)
            total += results[index];
        return total;
    }

    static async Task<int> SimulatedIoAsync(int value)
    {
        await Task.Delay(1).ConfigureAwait(false);
        return value;
    }
}
