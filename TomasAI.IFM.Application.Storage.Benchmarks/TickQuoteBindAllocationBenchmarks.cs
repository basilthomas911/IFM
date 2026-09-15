using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Application.Storage.MarketDataDb;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures the quote writer's fresh 21-slot parameter handoff with and without the binder copy.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class TickQuoteBindAllocationBenchmarks
{
    private readonly object _udtValue = new();

    [Benchmark(Baseline = true)]
    /// <summary>Builds a fresh parameter array and copies it as the borrowed binder did.</summary>
    public object?[] BorrowedParametersWithClone()
    {
        var values = CreateParameters();
        var resolved = (object?[])values.Clone();
        resolved[20] = _udtValue;
        return resolved;
    }

    [Benchmark]
    /// <summary>Transfers a fresh array exclusively to the prepared binder.</summary>
    public object?[] ExclusivelyOwnedParameters()
    {
        var parameter = new InsertTickQuoteData(CreateParameters());
        var owned = (TomasAI.IFM.Framework.Storage.ScyllaDb.IScyllaOwnedBindValues)parameter.Bind();
        var values = owned.TakeValues();
        values[20] = _udtValue;
        return values;
    }

    private static object?[] CreateParameters()
    {
        var values = new object?[21];
        values[20] = new object();
        return values;
    }
}
