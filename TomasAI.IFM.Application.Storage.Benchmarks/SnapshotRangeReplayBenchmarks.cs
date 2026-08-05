using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Newtonsoft.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>
/// Measures the managed replay work after the database has selected the rows. The before input matches the
/// current snapshot-to-stream-end query; the after input matches snapshot plus the last typed range returned
/// by the new SQL query. PostgreSQL selection is verified separately by integration tests.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class SnapshotRangeReplayBenchmarks
{
    const int LastNRange = 60;
    EventStreamReadModel[] _currentSnapshotRows = null!;
    EventStreamReadModel[] _boundedTypedRows = null!;

    [Params(256, 4096, 32768)]
    public int MatchingEventCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var snapshotJson = JsonConvert.SerializeObject(new FuturesRsiSignalStartedEvent
        {
            EntityId = EntityId,
            StartedOn = DateTime.UtcNow,
            StartedBy = "benchmark"
        });
        var generatedJson = JsonConvert.SerializeObject(new FuturesRsiSignalGeneratedEvent
        {
            EntityId = EntityId,
            FuturesRsiSignal = Signal,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "benchmark"
        });
        var noiseJson = JsonConvert.SerializeObject(new FuturesRsiSignalStoppedEvent
        {
            EntityId = EntityId,
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "benchmark"
        });

        _currentSnapshotRows = new EventStreamReadModel[(MatchingEventCount * 2) + 1];
        _currentSnapshotRows[0] = Row<FuturesRsiSignalStartedEvent>(1, snapshotJson);
        for (var index = 0; index < MatchingEventCount; index++)
        {
            _currentSnapshotRows[(index * 2) + 1] = Row<FuturesRsiSignalGeneratedEvent>((index * 2) + 2, generatedJson);
            _currentSnapshotRows[(index * 2) + 2] = Row<FuturesRsiSignalStoppedEvent>((index * 2) + 3, noiseJson);
        }

        var rangeCount = Math.Min(LastNRange, MatchingEventCount);
        _boundedTypedRows = new EventStreamReadModel[rangeCount + 1];
        _boundedTypedRows[0] = Row<FuturesRsiSignalStartedEvent>(1, snapshotJson);
        var firstMatchingIndex = MatchingEventCount - rangeCount;
        for (var index = 0; index < rangeCount; index++)
        {
            var sourceIndex = firstMatchingIndex + index;
            _boundedTypedRows[index + 1] = Row<FuturesRsiSignalGeneratedEvent>((sourceIndex * 2) + 2, generatedJson);
        }
    }

    [Benchmark(Baseline = true)]
    public FuturesRsiSignalCommandState BeforeCurrentSnapshotToEndReplay()
        => Replay(_currentSnapshotRows);

    [Benchmark]
    public FuturesRsiSignalCommandState AfterSnapshotLastTypedRangeReplay()
        => Replay(_boundedTypedRows);

    static FuturesRsiSignalCommandState Replay(EventStreamReadModel[] rows)
    {
        var state = new FuturesRsiSignalCommandState();
        state.ReplayEvents(rows);
        return state;
    }

    static EventStreamReadModel Row<TEvent>(long version, string eventData)
        => new()
        {
            EventVersion = version,
            EventTypeName = typeof(TEvent).AssemblyQualifiedName!,
            EventData = eventData
        };

    static readonly FuturesRsiSignalEntityId EntityId = new(
        "ESU6",
        new DateOnly(2026, 8, 5),
        TimeFrameType.Daily,
        60);

    static readonly FuturesRsiSignalReadModel Signal = new(
        contractId: "ESU6",
        valueDate: new DateOnly(2026, 8, 5),
        timePeriod: TimeFrameType.Daily,
        periodLength: 60,
        timestamp: new TimeOnly(10, 0),
        price: 5500m,
        priceChange: 1m,
        priceGain: 1m,
        priceLoss: 0m,
        averagePriceGain: 1m,
        averagePriceLoss: 0.5m,
        rs: 2,
        rsi: 66.67,
        rsiAverage: 65,
        rsiSlope: 0.1);
}
