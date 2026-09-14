using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks;

/// <summary>Measures the synchronous hot-path work added before the awaited Daily command request.</summary>
[MemoryDiagnoser]
[InProcess]
public class FuturesItiIngressBenchmarks
{
    readonly FuturesItiSignalRuntimeTelemetry telemetry = new(TimeProvider.System);
    readonly FuturesItiSignalGenerationGate busyGate = new();
    readonly Action noStartedAction = static () => { };
    readonly Func<ValueTask> completedGeneration = static () => ValueTask.CompletedTask;
    TaskCompletionSource busyGeneration = null!;
    FuturesMarketPriceUpdatedRealtimeEvent eligible = null!;
    FuturesMarketPriceUpdatedRealtimeEvent filtered = null!;

    /// <summary>Gets or sets the number of market events processed by one benchmark operation.</summary>
    [Params(1_000, 5_000, 10_000)]
    public int EventCount { get; set; }

    /// <summary>Creates stable eligible and filtered market-price events for every benchmark iteration.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var date = new DateOnly(2026, 9, 14);
        var timestamp = new DateTimeOffset(2026, 9, 14, 14, 30, 0, TimeSpan.Zero);
        var entity = new TickDataEntityId("ES20260918", date, AssetTypeId.Futures);
        eligible = new FuturesMarketPriceUpdatedRealtimeEvent
        {
            Subject = new(ActorType.Realtime, FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb, entity.Format()),
            Id = Guid.Parse("b3510c41-a94a-41a5-87fe-6d67f93245f9"),
            EntityId = entity,
            AggregateId = entity.Format(),
            EventSource = "benchmark",
            ReceivedOn = timestamp.UtcDateTime,
            UpdateSource = FuturesMarketPriceUpdateSource.Trade,
            Price = new FuturesMarketPriceSnapshot(entity.ContractId, 42, 7, AssetTypeId.Futures, date, null,
                new FuturesMarketTradeSnapshot(5450.25m, 5, 101, timestamp, timestamp,
                    NormalizedTradeAction.New, NormalizedTradeSide.Buy,
                    NormalizedTradeConditionFlags.None, Guid.Empty, 77))
        };
        filtered = eligible with { UpdateSource = FuturesMarketPriceUpdateSource.Quote };
        busyGeneration = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = busyGate.TryStart(noStartedAction, () => new ValueTask(busyGeneration.Task));
    }

    /// <summary>Releases the deliberately busy generation gate after all benchmarks complete.</summary>
    [GlobalCleanup]
    public void Cleanup() => busyGeneration.TrySetResult();

    /// <summary>Measures the allocation-free local eligibility predicate over the configured event count.</summary>
    /// <returns>The number of accepted trade events.</returns>
    [Benchmark(Baseline = true)]
    public int EligibilityFiltering()
    {
        var accepted = 0;
        for (var index = 0; index < EventCount; index++)
        {
            var item = (index & 1) == 0 ? eligible : filtered;
            if (FuturesMarketPriceUpdated.IsUsableTradeEvent(item, out _, out _))
                accepted++;
        }
        return accepted;
    }

    /// <summary>Measures all bounded telemetry writes used by an accepted ingress event.</summary>
    /// <returns>The final immutable telemetry snapshot.</returns>
    [Benchmark]
    public FuturesItiRuntimeSnapshot BoundedTelemetryUpdates()
    {
        var timestamp = eligible.Price.Trade!.Value.EventTimestamp.UtcDateTime;
        for (var index = 0; index < EventCount; index++)
        {
            telemetry.RecordMarketPriceReceived(timestamp);
            telemetry.RecordEligibleEsTrade(timestamp);
            telemetry.RecordCommandRequested();
            telemetry.RecordCommandAccepted();
        }
        return telemetry.GetSnapshot();
    }

    /// <summary>Measures immediate rejection of ticks while one generation operation is active.</summary>
    /// <returns>The number of ticks rejected by the busy gate.</returns>
    [Benchmark]
    public int BusyGateSkips()
    {
        var skipped = 0;
        for (var index = 0; index < EventCount; index++)
        {
            if (!busyGate.TryStart(noStartedAction, completedGeneration))
                skipped++;
        }
        return skipped;
    }

    /// <summary>Measures disabled source-generated Trace logging over the configured event count.</summary>
    [Benchmark]
    public void DisabledCommandGeneratedLogging()
    {
        for (var index = 0; index < EventCount; index++)
        {
            FuturesItiSignalRealtimeLogging.CommandGenerated(
                NullLogger.Instance, eligible.Id, eligible.CommandId,
                eligible.EntityId.ContractId, eligible.EntityId.ValueDate);
        }
    }
}
