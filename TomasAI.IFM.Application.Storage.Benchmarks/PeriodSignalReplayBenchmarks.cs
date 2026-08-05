using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Newtonsoft.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

public enum PeriodSignalVariant
{
    Rsi,
    RsiDaily,
    Macd,
    MacdDaily,
    Adx,
    AdxDaily,
    Atr,
    AtrDaily
}

/// <summary>
/// Compares managed reconstruction from a mixed, unbounded stream tail with reconstruction from the typed rows
/// selected by the database-bounded last-N queries. Database and network time are intentionally excluded.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class PeriodSignalReplayBenchmarks
{
    const int MatchingEventCount = 4096;
    const int LastNRange = 60;
    EventStreamReadModel[] _unboundedRows = null!;
    EventStreamReadModel[] _typedRangeRows = null!;
    Func<EventStreamReadModel[], object> _replay = null!;

    [ParamsAllValues]
    public PeriodSignalVariant Variant { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var descriptor = CreateDescriptor(Variant);
        var snapshotOffset = descriptor.SnapshotRow is null ? 0 : 1;
        _unboundedRows = new EventStreamReadModel[(MatchingEventCount * 2) + snapshotOffset];
        if (descriptor.SnapshotRow is not null)
            _unboundedRows[0] = descriptor.SnapshotRow;

        var noiseJson = JsonConvert.SerializeObject(new FuturesRsiSignalStoppedEvent
        {
            EntityId = RsiEntityId,
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "benchmark"
        });
        for (var index = 0; index < MatchingEventCount; index++)
        {
            var version = (index * 2) + snapshotOffset + 1;
            _unboundedRows[(index * 2) + snapshotOffset] = descriptor.CreateEventRow(version);
            _unboundedRows[(index * 2) + snapshotOffset + 1] =
                Row<FuturesRsiSignalStoppedEvent>(version + 1, noiseJson);
        }

        _typedRangeRows = new EventStreamReadModel[LastNRange + snapshotOffset];
        if (descriptor.SnapshotRow is not null)
            _typedRangeRows[0] = descriptor.SnapshotRow;
        var firstMatchingIndex = MatchingEventCount - LastNRange;
        for (var index = 0; index < LastNRange; index++)
        {
            var sourceIndex = firstMatchingIndex + index;
            var version = (sourceIndex * 2) + snapshotOffset + 1;
            _typedRangeRows[index + snapshotOffset] = descriptor.CreateEventRow(version);
        }

        _replay = descriptor.Replay;
    }

    [Benchmark(Baseline = true)]
    public object BeforeUnboundedMixedReplay() => _replay(_unboundedRows);

    [Benchmark]
    public object AfterTypedLastNReplay() => _replay(_typedRangeRows);

    static ReplayDescriptor CreateDescriptor(PeriodSignalVariant variant)
        => variant switch
        {
            PeriodSignalVariant.Rsi => Descriptor<FuturesRsiSignalGeneratedEvent>(
                new FuturesRsiSignalGeneratedEvent
                {
                    EntityId = RsiEntityId,
                    FuturesRsiSignal = RsiSignal,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = "benchmark"
                },
                ReplayRsi,
                Row<FuturesRsiSignalStartedEvent>(0, JsonConvert.SerializeObject(new FuturesRsiSignalStartedEvent
                {
                    EntityId = RsiEntityId,
                    StartedOn = DateTime.UtcNow,
                    StartedBy = "benchmark"
                }))),
            PeriodSignalVariant.RsiDaily => Descriptor<FuturesRsiDailySignalGeneratedEvent>(
                new FuturesRsiDailySignalGeneratedEvent
                {
                    EntityId = RsiDailyEntityId,
                    FuturesRsiSignal = RsiSignal,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = "benchmark"
                },
                ReplayRsi),
            PeriodSignalVariant.Macd => Descriptor<FuturesMacdSignalGeneratedEvent>(
                new FuturesMacdSignalGeneratedEvent
                {
                    EntityId = MacdEntityId,
                    FuturesMacdSignal = MacdSignal,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = "benchmark"
                },
                ReplayMacd),
            PeriodSignalVariant.MacdDaily => Descriptor<FuturesMacdDailySignalGeneratedEvent>(
                new FuturesMacdDailySignalGeneratedEvent
                {
                    EntityId = MacdDailyEntityId,
                    FuturesMacdSignal = MacdSignal,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = "benchmark"
                },
                ReplayMacd),
            PeriodSignalVariant.Adx => Descriptor<FuturesAdxSignalGeneratedEvent>(
                new FuturesAdxSignalGeneratedEvent
                {
                    EntityId = AdxEntityId,
                    FuturesAdxSignal = AdxSignal,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = "benchmark"
                },
                ReplayAdx),
            PeriodSignalVariant.AdxDaily => Descriptor<FuturesAdxDailySignalGeneratedEvent>(
                new FuturesAdxDailySignalGeneratedEvent
                {
                    EntityId = AdxDailyEntityId,
                    FuturesAdxSignal = AdxSignal,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = "benchmark"
                },
                ReplayAdx),
            PeriodSignalVariant.Atr => Descriptor<FuturesAtrSignalGeneratedEvent>(
                new FuturesAtrSignalGeneratedEvent
                {
                    EntityId = AtrEntityId,
                    FuturesAtrSignal = AtrSignal,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = "benchmark"
                },
                ReplayAtr),
            PeriodSignalVariant.AtrDaily => Descriptor<FuturesAtrDailySignalGeneratedEvent>(
                new FuturesAtrDailySignalGeneratedEvent
                {
                    EntityId = AtrDailyEntityId,
                    FuturesAtrSignal = AtrSignal,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = "benchmark"
                },
                ReplayAtr),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null)
        };

    static ReplayDescriptor Descriptor<TEvent>(
        TEvent @event,
        Func<EventStreamReadModel[], object> replay,
        EventStreamReadModel? snapshotRow = null)
        => new(
            version => Row<TEvent>(version, JsonConvert.SerializeObject(@event)),
            replay,
            snapshotRow);

    static object ReplayRsi(EventStreamReadModel[] rows)
    {
        var state = new FuturesRsiSignalCommandState();
        state.ReplayEvents(rows);
        return state;
    }

    static object ReplayMacd(EventStreamReadModel[] rows)
    {
        var state = new FuturesMacdSignalCommandState();
        state.ReplayEvents(rows);
        return state;
    }

    static object ReplayAdx(EventStreamReadModel[] rows)
    {
        var state = new FuturesAdxSignalCommandState();
        state.ReplayEvents(rows);
        return state;
    }

    static object ReplayAtr(EventStreamReadModel[] rows)
    {
        var state = new FuturesAtrSignalCommandState();
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

    sealed record ReplayDescriptor(
        Func<long, EventStreamReadModel> CreateEventRow,
        Func<EventStreamReadModel[], object> Replay,
        EventStreamReadModel? SnapshotRow);

    static readonly DateOnly ValueDate = new(2026, 8, 5);
    static readonly TimeOnly Timestamp = new(10, 0);
    static readonly FuturesRsiSignalEntityId RsiEntityId = new("ESU6", ValueDate, TimeFrameType.Daily, LastNRange);
    static readonly FuturesRsiDailySignalEntityId RsiDailyEntityId = new("ESU6", TimeFrameType.Daily, LastNRange);
    static readonly FuturesMacdSignalEntityId MacdEntityId = new("ESU6", ValueDate, TimeFrameType.Daily, LastNRange);
    static readonly FuturesMacdDailySignalEntityId MacdDailyEntityId = new("ESU6", TimeFrameType.Daily, LastNRange);
    static readonly FuturesAdxSignalEntityId AdxEntityId = new("ESU6", ValueDate, TimeFrameType.Daily, LastNRange);
    static readonly FuturesAdxDailySignalEntityId AdxDailyEntityId = new("ESU6", TimeFrameType.Daily, LastNRange);
    static readonly FuturesAtrSignalEntityId AtrEntityId = new("ESU6", ValueDate, TimeFrameType.Daily, LastNRange);
    static readonly FuturesAtrDailySignalEntityId AtrDailyEntityId = new("ESU6", TimeFrameType.Daily, LastNRange);

    static readonly FuturesRsiSignalReadModel RsiSignal = new(
        "ESU6", ValueDate, TimeFrameType.Daily, LastNRange, Timestamp, 5500m,
        1m, 1m, 0m, 1m, 0.5m, 2, 66.67, 65, 0.1);

    static readonly FuturesMacdSignalReadModel MacdSignal = new(
        "ESU6", ValueDate, TimeFrameType.Daily, LastNRange, Timestamp, 5500m,
        1.5, 1.2, 0.3, FuturesTrendDirectionType.UpTrending, FuturesTrendDirectionStrengthType.Medium);

    static readonly FuturesAdxSignalReadModel AdxSignal = new(
        "ESU6", ValueDate, TimeFrameType.Daily, LastNRange, Timestamp, 5500m,
        25, 15, 30, FuturesTrendDirectionType.UpTrending, FuturesTrendDirectionStrengthType.Medium);

    static readonly FuturesAtrSignalReadModel AtrSignal = new(
        "ESU6", ValueDate, TimeFrameType.Daily, LastNRange, Timestamp, 5500m,
        1, 1.5, FuturesTrendDirectionType.UpTrending, FuturesTrendDirectionStrengthType.Medium);
}
