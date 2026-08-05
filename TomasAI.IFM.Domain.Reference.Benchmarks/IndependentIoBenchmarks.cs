using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

namespace TomasAI.IFM.Domain.Reference.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class IndependentIoBenchmarks
{
    [Params(3, 6)]
    public int Operations { get; set; }

    [Benchmark(Baseline = true)]
    public async Task<int> BeforeSequential()
    {
        var result = 0;
        for (var i = 0; i < Operations; i++)
            result += await ReadAsync(i).ConfigureAwait(false);
        return result;
    }

    [Benchmark]
    public async Task<int> AfterConcurrent()
    {
        var tasks = new Task<int>[Operations];
        for (var i = 0; i < tasks.Length; i++)
            tasks[i] = ReadAsync(i);
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.Sum();
    }

    static async Task<int> ReadAsync(int value)
    {
        await Task.Delay(1).ConfigureAwait(false);
        return value;
    }
}
