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
    : BaseRealtimeProjector<FuturesEodDataRealtimeActor>(logger), IAsyncDisposable
{
    readonly bool buffered = bool.TryParse(configuration?["MarketData:FuturesEodMinuteBatchWriter"], out var enabled) && enabled;
    readonly SemaphoreSlim wake = new(0, 1);
    readonly System.Threading.Lock disposalGate = new();
    Task? disposal;

    /// <summary>Completes the final writer shutdown once and releases its wake semaphore.</summary>
    public ValueTask DisposeAsync()
    {
        lock (disposalGate) return new ValueTask(disposal ??= DisposeCoreAsync());
    }

    async Task DisposeCoreAsync()
    {
        try { await StopAsync().ConfigureAwait(false); }
        finally
        {
            spool?.Dispose();
            spool = null;
            wake.Dispose();
        }
    }
    EodHistorySpool? spool;
    Task? writer;
    int stopping;
    readonly TimeSpan retryDelay = TimeSpan.FromSeconds(2);
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")] 
    /// <summary>Starts the independent history writer. Pending disk records survive actor/process restart.</summary>
    public override async ValueTask StartAsync(IEventActorContext context, CancellationToken cancellationToken = default)
    {
        await (base.StartAsync(context, cancellationToken)).ConfigureAwait(false);
        if (buffered && writer is null)
        {
            Volatile.Write(ref stopping, 0);
            try
            {
                spool = new(configuration?["MarketData:FuturesEodHistorySpoolPath"]
                    ?? Path.Combine(AppContext.BaseDirectory, "History", "FuturesEod"));
                using var persistenceScope = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterIndependentPersistence();
                writer = WriteBatchesAsync();
                if (spool.Pending > 0) SignalWriter();
            }
            catch (Exception exception)
            {
                global::TomasAI.IFM.Domain.MarketData.Feed.ActorLifecycleDiagnosticLog.Write3(logger, exception, nameof(StartAsync));
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
            await (writer.WaitAsync(cancellationToken)).ConfigureAwait(false);
            writer = null;
        }
        spool?.Dispose();
        spool = null;
        await (base.StopAsync(cancellationToken)).ConfigureAwait(false);
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")] 
    /// <summary>Updates the live cache before any history work; database failure cannot block live publication.</summary>
    public override async ValueTask<bool> ProcessRealtimeEventAsync(IEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!buffered) return await (base.ProcessRealtimeEventAsync(domainEvent, cancellationToken)).ConfigureAwait(false);
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
        try { await (PublishRealtimeEventAsync(Context, domainEvent, ActorName, cancellationToken)).ConfigureAwait(false); }
        catch (Exception exception)
        {
            global::TomasAI.IFM.Domain.MarketData.Feed.ActorLifecycleDiagnosticLog.Write4(logger, exception, nameof(ProcessRealtimeEventAsync), domainEvent.Id);
        }
        try
        {
            if (spool is null) throw new IOException("EOD history spool is unavailable.");
            await (spool.AppendAsync(EodHistoryEntry.Create(domainEvent, row, vx, version))).ConfigureAwait(false);
            if (spool.Pending >= 512) SignalWriter();
        }
        catch (Exception exception)
        {
            EodHistoryMetrics.Failures.Add(1, new KeyValuePair<string, object?>("stage", "capture"));
            // History is secondary. Explicitly report local disk failure without stopping trading.
            global::TomasAI.IFM.Domain.MarketData.Feed.ActorLifecycleDiagnosticLog.Write5(logger, exception, nameof(ProcessRealtimeEventAsync), domainEvent.Id);
        }
        return true;
    }

    void SignalWriter()
    {
        try { wake.Release(); } catch (SemaphoreFullException)
        {
            // A writer wakeup is already pending; another signal is unnecessary.
        }
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")] 
    async Task WriteBatchesAsync()
    {
        var history = spool!;
        while (true)
        {
            await (wake.WaitAsync(TimeSpan.FromMinutes(1))).ConfigureAwait(false);
            List<EodHistoryEntry> batch;
            try { batch = await (history.ReadBatchAsync(512)).ConfigureAwait(false); }
            catch (Exception exception)
            {
                global::TomasAI.IFM.Domain.MarketData.Feed.ActorLifecycleDiagnosticLog.Write6(logger, exception, nameof(WriteBatchesAsync));
                if (Volatile.Read(ref stopping) != 0) return;
                await DelayRetryAsync().ConfigureAwait(false);
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
                await PersistBatchAsync(batch).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                EodHistoryMetrics.Failures.Add(1, new KeyValuePair<string, object?>("stage", "persist"));
                global::TomasAI.IFM.Domain.MarketData.Feed.ActorLifecycleDiagnosticLog.Write7(logger, exception, nameof(WriteBatchesAsync), batch.Count, history.Pending);
                if (Volatile.Read(ref stopping) != 0) return;
                await DelayRetryAsync().ConfigureAwait(false);
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
                    await (PublishRealtimeEventAsync(Context, descriptor.CompletedEventFactory(source)!, ActorName, CancellationToken.None)).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    EodHistoryMetrics.Failures.Add(1, new KeyValuePair<string, object?>("stage", "notification"));
                    global::TomasAI.IFM.Domain.MarketData.Feed.ActorLifecycleDiagnosticLog.Write8(logger, exception, nameof(WriteBatchesAsync), source.Id);
                }
                try { history.Acknowledge(item); }
                catch (Exception exception)
                {
                    global::TomasAI.IFM.Domain.MarketData.Feed.ActorLifecycleDiagnosticLog.Write9(logger, exception, nameof(WriteBatchesAsync), source.Id);
                }
            }
            RecordBatchCompletion(history, batch.Count, started);
            if (history.Pending >= 512 || Volatile.Read(ref stopping) != 0) SignalWriter();
        }
    }

    /// <summary>Waits the configured failed-batch delay and wakes the existing history writer.</summary>
    async Task DelayRetryAsync()
    {
        await Task.Delay(retryDelay).ConfigureAwait(false);
        SignalWriter();
    }

    /// <summary>Records the completed history batch count, pending backlog and elapsed persistence/publication duration.</summary>
    void RecordBatchCompletion(EodHistorySpool history, int count, long started)
    {
            EodHistoryMetrics.BatchDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            global::TomasAI.IFM.Domain.MarketData.Feed.ActorLifecycleDiagnosticLog.Write10(logger, null, nameof(WriteBatchesAsync), count, history.Pending, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    /// <summary>Persists allocated ES history IDs before row mutations and coalesces VX observations by contract and value date.</summary>
    async Task PersistBatchAsync(List<EodHistoryEntry> batch)
    {
                var rows = batch.Where(item => item.Row is not null).Select(item => item.Row!).ToArray();
                if (rows.Length > 0)
                {
                    await (dbFactory.MarketDataDb.PrepareRealtimeFuturesEodBatchAsync(rows)).ConfigureAwait(false);
                    // Persist allocated IDs before submitting mutations, including before an uncertain response.
                    foreach (var item in batch.Where(item => item.Row is not null)) await (EodHistorySpool.SaveAsync(item)).ConfigureAwait(false);
                    await (dbFactory.MarketDataDb.PersistRealtimeFuturesEodBatchAsync(rows)).ConfigureAwait(false);
                }
                var vxRows = batch.Where(item => item.Vx is not null)
                    .GroupBy(item => (item.Vx!.ContractId, item.Vx.ValueDate)).Select(group => group.Last().Vx!).ToArray();
                if (vxRows.Length > 0) await (dbFactory.MarketDataDb.PersistRealtimeVixEodBatchAsync(vxRows)).ConfigureAwait(false);
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
}
