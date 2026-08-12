using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class EconomicCalendarImportBenchmarks
{
    EconomicCalendarReadModel[] _values = default!;

    [Params(8, 64, 512)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var date = new DateTime(2026, 8, 5, 8, 30, 0);
        _values = Enumerable.Range(0, Rows)
            .Select(i => new EconomicCalendarReadModel(
                date.AddMinutes(i), "US", $"Event {i}", "1", "1", "1", date, "benchmark"))
            .ToArray();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> BeforePerRowEventAndWrite()
    {
        var processed = 0;
        foreach (var value in _values)
        {
            _ = new EconomicCalendarAddedEvent
            {
                EntityId = value.Id,
                EconomicCalendar = value
            };
            await SimulatedWriteAsync().ConfigureAwait(false);
            processed++;
        }
        return processed;
    }

    [Benchmark]
    public async Task<int> AfterBatchEventAndWrite()
    {
        _ = new EconomicCalendarsImportedEvent { EconomicCalendars = _values };
        await SimulatedWriteAsync().ConfigureAwait(false);
        return _values.Length;
    }

    static async ValueTask SimulatedWriteAsync() => await Task.Yield();
}
