using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class YieldCurveRateStateBenchmarks
{
    IEvent[] _events = null!;

    [Params(32, 256, 2048)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var start = new DateOnly(2000, 1, 1);
        _events = Enumerable.Range(0, Count)
            .Select(index => (IEvent)new YieldCurveRatesImportedEvent
            {
                ImportDate = start.AddDays(index).ToDateTime(TimeOnly.MinValue),
                RequestedOn = DateTime.UtcNow,
                RequestedBy = "benchmark"
            })
            .ToArray();
    }

    [Benchmark]
    public YieldCurveRateCommandState ReplayImportSnapshot()
    {
        var state = new YieldCurveRateCommandState();
        state.ReplayEvents(_events);
        return state;
    }
}

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class YieldCurveRateValidationBenchmarks
{
    YieldCurveRateReadModel[] _rates = null!;
    YieldCurveRateValidationRules _rules = null!;

    [Params(1, 32, 256)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _rates = BenchmarkData.CreateRates(Count);
        _rules = new YieldCurveRateValidationRules();
    }

    [Benchmark]
    public int ValidateImport()
    {
        var errorCount = 0;
        foreach (var rate in _rates)
            errorCount += _rules.Execute(rate).Length;
        return errorCount;
    }
}

static class BenchmarkData
{
    public static YieldCurveRateReadModel[] CreateRates(int count)
    {
        var rates = new YieldCurveRateReadModel[count];
        var start = new DateOnly(2000, 1, 1);
        for (var index = 0; index < rates.Length; index++)
        {
            rates[index] = new YieldCurveRateReadModel(
                start.AddDays(index),
                1, 1.1, 1.2, 1.3,
                1.4, 1.5, 1.6, 1.7,
                1.8, 1.9, 2.0, 2.1);
        }
        return rates;
    }
}
