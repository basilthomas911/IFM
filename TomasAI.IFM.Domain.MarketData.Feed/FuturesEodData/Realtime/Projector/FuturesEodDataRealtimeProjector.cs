using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;

/// <summary>
/// Applies rolling futures and VIX EOD observations once, without event-log or
/// projection replay infrastructure.
/// </summary>
public sealed class FuturesEodDataRealtimeProjector(
    IDbContextFactory dbFactory,
    ILogger<FuturesEodDataRealtimeProjector> logger, IConfiguration? configuration = null)
    : BaseRealtimeProjector<FuturesEodDataRealtimeActor>(logger)
{
    readonly bool buffered = bool.TryParse(configuration?["MarketData:FuturesEodMinuteBatchWriter"], out var enabled) && enabled;
    readonly SemaphoreSlim wake = new(0, 1);
    EodHistorySpool? spool;
    Task? writer;
    int stopping;
    readonly TimeSpan retryDelay = TimeSpan.FromSeconds(2);

    /// <summary>Starts the independent history writer. Pending disk records survive actor/process restart.</summary>
    public override async ValueTask StartAsync(IEventActorContext context, CancellationToken cancellationToken = default)
    {
        await base.StartAsync(context, cancellationToken);
        if (buffered && writer is null)
        {
            Volatile.Write(ref stopping, 0);
            try
            {
                spool = new(configuration?["MarketData:FuturesEodHistorySpoolPath"]
                    ?? Path.Combine(AppContext.BaseDirectory, "History", "FuturesEod"));
                writer = WriteBatchesAsync();
                if (spool.Pending > 0) SignalWriter();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "{Method} EOD history spool unavailable; realtime cache remains active", nameof(StartAsync));
            }
        }
    }

    /// <summary>Attempts the final history flush; outages retain disk records instead of preventing shutdown.</summary>
    public override async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (writer is not null)
        {
            Volatile.Write(ref stopping, 1);
            SignalWriter();
            await writer.WaitAsync(cancellationToken);
            writer = null;
        }
        spool?.Dispose();
        spool = null;
        await base.StopAsync(cancellationToken);
    }

    /// <summary>Updates the live cache before any history work; database failure cannot block live publication.</summary>
    public override async ValueTask<bool> ProcessRealtimeEventAsync(IEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (!buffered) return await base.ProcessRealtimeEventAsync(domainEvent, cancellationToken);
        _ = Context;
        BufferedFuturesEodRow? row = domainEvent switch
        {
            FuturesEodDataInsertedEvent e => new(e.FuturesEodData, true),
            FuturesEodSessionStatisticsUpdatedEvent e => new(e.FuturesEodData, false),
            VixFuturesEodDataInsertedEvent => null,
            _ => throw new InvalidOperationException($"Unsupported EOD event: {domainEvent.GetType().Name}")
        };
        var version = row is null ? 0 : CurrentFuturesEodCache.Shared.Publish(row.Snapshot);
        VixFuturesEodDataReadModel? vx = null;
        if (domainEvent is VixFuturesEodDataInsertedEvent vix)
        {
            var tick = vix.VixFuturesTickData;
            // A cold VX observation starts from its tick/statistics; storage is never consulted on this path.
            CurrentVixEodCache.Shared.TryGet(tick.ContractId, tick.ValueDate, out var previous);
            var statistics = vix.SessionStatistics;
            var prices = statistics is { HasPriceStatistics: true };
            vx = new(tick.ContractId, tick.ValueDate,
                prices ? statistics!.Value.OpenPrice : previous?.OpenPrice ?? tick.Price,
                prices ? statistics!.Value.HighPrice : Math.Max(previous?.HighPrice ?? tick.Price, tick.Price),
                prices ? statistics!.Value.LowPrice : Math.Min(previous?.LowPrice ?? tick.Price, tick.Price),
                tick.Price, statistics is { HasVolume: true } ? statistics.Value.Volume : checked((previous?.Volume ?? 0) + tick.Size));
            CurrentVixEodCache.Shared.Publish(vx);
        }
        try { await PublishRealtimeEventAsync(Context, domainEvent, ActorName, cancellationToken); }
        catch (Exception exception)
        {
            logger.LogError(exception, "{Method} EOD live publication failed; EventId={EventId}; cache remains updated",
                nameof(ProcessRealtimeEventAsync), domainEvent.Id);
        }
        try
        {
            if (spool is null) throw new IOException("EOD history spool is unavailable.");
            await spool.AppendAsync(EodHistoryEntry.Create(domainEvent, row, vx, version));
            if (spool.Pending >= 512) SignalWriter();
        }
        catch (Exception exception)
        {
            EodHistoryMetrics.Failures.Add(1, new KeyValuePair<string, object?>("stage", "capture"));
            // History is secondary. Explicitly report local disk failure without stopping trading.
            logger.LogCritical(exception, "{Method} EOD history capture failed; EventId={EventId}; Contract history may have a gap; realtime cache remains updated",
                nameof(ProcessRealtimeEventAsync), domainEvent.Id);
        }
        return true;
    }

    void SignalWriter()
    {
        try { wake.Release(); } catch (SemaphoreFullException) { }
    }

    async Task WriteBatchesAsync()
    {
        var history = spool!;
        while (true)
        {
            await wake.WaitAsync(TimeSpan.FromMinutes(1));
            List<EodHistoryEntry> batch;
            try { batch = await history.ReadBatchAsync(512); }
            catch (Exception exception)
            {
                logger.LogError(exception, "{Method} EOD history spool read failed; live cache remains active", nameof(WriteBatchesAsync));
                if (Volatile.Read(ref stopping) != 0) return;
                await Task.Delay(retryDelay);
                SignalWriter();
                continue;
            }
            if (batch.Count == 0)
            {
                if (Volatile.Read(ref stopping) != 0) return;
                continue;
            }
            var started = Stopwatch.GetTimestamp();
            EodHistoryMetrics.OldestAge.Record(Math.Max(0, (DateTime.UtcNow - batch.Min(item => item.ObservedAtUtc)).TotalSeconds));
            try
            {
                var rows = batch.Where(item => item.Row is not null).Select(item => item.Row!).ToArray();
                if (rows.Length > 0)
                {
                    await dbFactory.MarketDataDb.PrepareRealtimeFuturesEodBatchAsync(rows);
                    // Persist allocated IDs before submitting mutations, including before an uncertain response.
                    foreach (var item in batch.Where(item => item.Row is not null)) await EodHistorySpool.SaveAsync(item);
                    await dbFactory.MarketDataDb.PersistRealtimeFuturesEodBatchAsync(rows);
                }
                var vxRows = batch.Where(item => item.Vx is not null)
                    .GroupBy(item => (item.Vx!.ContractId, item.Vx.ValueDate)).Select(group => group.Last().Vx!).ToArray();
                if (vxRows.Length > 0) await dbFactory.MarketDataDb.PersistRealtimeVixEodBatchAsync(vxRows);
            }
            catch (Exception exception)
            {
                EodHistoryMetrics.Failures.Add(1, new KeyValuePair<string, object?>("stage", "persist"));
                logger.LogError(exception, "{Method} EOD history batch retained on disk; RowCount={RowCount}; Pending={Pending}; live cache remains active",
                    nameof(WriteBatchesAsync), batch.Count, history.Pending);
                if (Volatile.Read(ref stopping) != 0) return;
                await Task.Delay(retryDelay);
                SignalWriter();
                continue;
            }
            foreach (var item in batch)
            {
                var source = item.Event;
                // Process-local versions from a recovered spool must not mark a new process's cache as persisted.
                if (item.CacheOwnerId == CurrentFuturesEodCache.Shared.OwnerId && item.Row is not null && CurrentFuturesEodCache.Shared.TryGet(item.Row.Snapshot.ContractId,
                    item.Row.Snapshot.ValueDate, out var current) && current == item.Row.Snapshot)
                    CurrentFuturesEodCache.Shared.MarkPersisted(item.Row.Snapshot.ContractId, item.Row.Snapshot.ValueDate, item.CacheVersion);
                try
                {
                    var descriptor = ProjectionDescriptors.Single(value => value.SourceEventType == source.GetType());
                    await PublishRealtimeEventAsync(Context, descriptor.CompletedEventFactory(source)!, ActorName, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    EodHistoryMetrics.Failures.Add(1, new KeyValuePair<string, object?>("stage", "notification"));
                    logger.LogWarning(exception, "{Method} Persisted EOD completion notification failed; EventId={EventId}; history writer continues",
                        nameof(WriteBatchesAsync), source.Id);
                }
                try { history.Acknowledge(item); }
                catch (Exception exception)
                {
                    logger.LogError(exception, "{Method} Persisted EOD spool acknowledgement failed; EventId={EventId}", nameof(WriteBatchesAsync), source.Id);
                }
            }
            EodHistoryMetrics.BatchDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            logger.LogInformation("{Method} EOD history batch persisted; RowCount={RowCount}; Pending={Pending}; ElapsedMilliseconds={ElapsedMilliseconds}",
                nameof(WriteBatchesAsync), batch.Count, history.Pending, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (history.Pending >= 512 || Volatile.Read(ref stopping) != 0) SignalWriter();
        }
    }

    readonly ImmutableArray<RealtimeProjectionDescriptor> _descriptors =
    [
        Describe<
            FuturesEodDataInsertedEvent,
            FuturesEodDataInsertedCompleteEvent,
            FuturesEodDataInsertedFailEvent,
            FuturesEodDataId>(e => dbFactory.MarketDataDb.InsertFuturesEodDataAsync(
                e.FuturesEodData)),
        Describe<
            FuturesEodSessionStatisticsUpdatedEvent,
            FuturesEodDataInsertedCompleteEvent,
            FuturesEodDataInsertedFailEvent,
            FuturesEodDataId>(e => dbFactory.MarketDataDb.InsertFuturesEodDataAsync(
                e.FuturesEodData)),
        Describe<
            VixFuturesEodDataInsertedEvent,
            VixFuturesEodDataInsertedCompleteEvent,
            VixFuturesEodDataInsertedFailEvent,
            FuturesEodDataId>(e => dbFactory.MarketDataDb.InsertVixFuturesEodDataAsync(
                e.VixFuturesTickData,
                e.SessionStatistics))
    ];

    public override string ActorName => FuturesEodDataRealtimeActor.ActorName;
    public override string ProjectorName => nameof(FuturesEodDataRealtimeProjector);
    protected override TimeSpan SlowStageLogThreshold => TimeSpan.FromMilliseconds(250);
    public override IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors =>
        _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
