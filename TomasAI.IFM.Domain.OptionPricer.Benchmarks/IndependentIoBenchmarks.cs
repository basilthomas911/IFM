using BenchmarkDotNet.Attributes;

namespace TomasAI.IFM.Domain.OptionPricer.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 8)]
public class IndependentIoBenchmarks
{
    [Benchmark(Baseline = true)]
    public async Task<int> SequentialRequests()
    {
        var first = await SimulatedActorRequestAsync();
        var second = await SimulatedActorRequestAsync();
        return first + second;
    }

    [Benchmark]
    public async Task<int> OverlappedRequests()
    {
        var firstPending = SimulatedActorRequestAsync();
        var secondPending = SimulatedActorRequestAsync();
        var first = await firstPending;
        var second = await secondPending;
        return first + second;
    }

    static async ValueTask<int> SimulatedActorRequestAsync()
    {
        await Task.Delay(2).ConfigureAwait(false);
        return 1;
    }
}
