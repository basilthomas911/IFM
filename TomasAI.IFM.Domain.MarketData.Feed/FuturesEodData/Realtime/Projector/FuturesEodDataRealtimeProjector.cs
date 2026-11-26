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
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;

/// <summary>
/// Applies rolling futures and VIX EOD observations. Buffered mode durably owns
/// frozen results and resumes required publication and idempotent persistence.
/// </summary>
public sealed class FuturesEodDataRealtimeProjector(
    IDbContextFactory dbFactory, ISequenceIdGenerator sequences,
    ILogger<FuturesEodDataRealtimeProjector> logger, IConfiguration? configuration = null)
    : BaseRealtimeProjector<FuturesEodDataRealtimeActor>(logger), IAsyncDisposable
{
    readonly bool buffered = bool.TryParse(configuration?["MarketData:FuturesEodMinuteBatchWriter"], out var enabled) && enabled;
    readonly SemaphoreSlim wake = new(0, 1), captureGate = new(1, 1);
    readonly System.Threading.Lock disposalGate = new();
    Task? disposal, writer;
    EodCaptureJournal? journal;
    int stopping;
    readonly TimeSpan retryDelay = TimeSpan.FromSeconds(2);
    internal int PendingCaptures => journal?.Pending ?? 0;

    public ValueTask DisposeAsync()
    {
        lock (disposalGate) return new ValueTask(disposal ??= DisposeCoreAsync());
    }
    async Task DisposeCoreAsync()
    {
        try { await StopAsync().ConfigureAwait(false); }
        finally { journal?.Dispose(); journal = null; wake.Dispose(); captureGate.Dispose(); }
    }
    public override async ValueTask StartAsync(IEventActorContext context, CancellationToken cancellationToken = default)
    {
        await base.StartAsync(context, cancellationToken).ConfigureAwait(false);
        if (!buffered || writer is not null) return;
        try
        {
            var directory = configuration?["MarketData:FuturesEodHistorySpoolPath"] ?? Path.Combine(AppContext.BaseDirectory, "History", "FuturesEod");
            journal = new(directory);
            // Upgrade old history records before admitting fresh results. Each new receipt is durable before retiring its old owner.
            using (var legacy = new EodHistorySpool(directory))
            {
                while (legacy.Pending > 0)
                {
                    var batch = await legacy.ReadBatchAsync(512).ConfigureAwait(false);
                    if (batch.Count == 0) throw new InvalidDataException("EOD legacy spool cannot advance.");
                    foreach (var item in batch)
                    {
                        FreezeCompletion(item);
                        var legacyEntry = new EodHistoryEntry { FileName = item.FileName, Segment = item.Segment, SegmentIndex = item.SegmentIndex };
                        if (await journal.FindAsync(item.Event.Id).ConfigureAwait(false) is null) await journal.PrepareAsync(item).ConfigureAwait(false);
                        legacy.Acknowledge(legacyEntry);
                    }
                }
            }
            foreach (var item in journal.Batch(4096)) PublishCache(item);
            Volatile.Write(ref stopping, 0);
            using var persistenceScope = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterIndependentPersistence();
            writer = WriteBatchesAsync();
            if (journal.Pending > 0) SignalWriter();
        }
        catch
        {
            journal?.Dispose(); journal = null;
            await base.StopAsync(CancellationToken.None).ConfigureAwait(false);
            throw; // A projector without a durable capture owner must not start successfully.
        }
    }
    public override async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (!buffered) { await base.StopAsync(cancellationToken).ConfigureAwait(false); return; }
        await captureGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { Volatile.Write(ref stopping, 1); }
        finally { captureGate.Release(); }
        if (writer is not null)
        {
            SignalWriter();
            // Never dispose ownership while a database mutation/publication is still running.
            await writer.ConfigureAwait(false); writer = null;
        }
        journal?.Dispose(); journal = null;
        await base.StopAsync(CancellationToken.None).ConfigureAwait(false);
    }
    public override async ValueTask<bool> ProcessRealtimeEventAsync(IEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!buffered) return await base.ProcessRealtimeEventAsync(domainEvent, cancellationToken).ConfigureAwait(false);
        _ = Context;
        cancellationToken.ThrowIfCancellationRequested();
        if (domainEvent.Id == Guid.Empty || domainEvent.CommandId == Guid.Empty) throw new InvalidDataException("EOD capture requires stable event and command identities.");
        await captureGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref stopping) != 0 || journal is null) throw new InvalidOperationException("EOD capture intake is unavailable.");
            var existing = await journal.FindAsync(domainEvent.Id).ConfigureAwait(false);
            if (existing is not null)
            {
                var original = existing.Event;
                if (original.GetType() != domainEvent.GetType() || original.CommandId != domainEvent.CommandId || original.Subject.EntityId != domainEvent.Subject.EntityId)
                    throw new InvalidDataException("EOD capture identity conflicts with its frozen result.");
                if (existing.Stage < 3 && existing.CacheVersion == 0) PublishCache(existing);
                return true; // The original frozen result already owns any unfinished work.
            }
            BufferedFuturesEodRow? row = domainEvent switch
            {
                FuturesEodDataInsertedEvent e => new(e.FuturesEodData, true),
                FuturesEodSessionStatisticsUpdatedEvent e => new(e.FuturesEodData, false),
                VixFuturesEodDataInsertedEvent => null,
                _ => throw new InvalidOperationException($"Unsupported EOD event: {domainEvent.GetType().Name}")
            };
            VixFuturesEodDataReadModel? vx = null;
            if (domainEvent is VixFuturesEodDataInsertedEvent vix)
            {
                var tick = vix.VixFuturesTickData;
                CurrentVixEodCache.Shared.TryGet(tick.ContractId, tick.ValueDate, out var previous);
                var statistics = vix.SessionStatistics;
                var prices = statistics is { HasPriceStatistics: true };
                vx = new(tick.ContractId, tick.ValueDate,
                    prices ? statistics!.Value.OpenPrice : previous?.OpenPrice ?? tick.Price,
                    prices ? statistics!.Value.HighPrice : Math.Max(previous?.HighPrice ?? tick.Price, tick.Price),
                    prices ? statistics!.Value.LowPrice : Math.Min(previous?.LowPrice ?? tick.Price, tick.Price),
                    tick.Price, statistics is { HasVolume: true } ? statistics.Value.Volume : checked((previous?.Volume ?? 0) + tick.Size));
            }
            var entry = EodHistoryEntry.Create(domainEvent, row, vx, 0);
            FreezeCompletion(entry);
            await journal.PrepareAsync(entry).ConfigureAwait(false);
            PublishCache(entry); // Never advance cache/no-change state before durable ownership.
            if (journal.Pending >= 512) SignalWriter();
            return true; // Durable handoff, not a claim that database/publication already completed.
        }
        finally { captureGate.Release(); }
    }
    void FreezeCompletion(EodHistoryEntry entry)
    {
        if (entry.CompletionBytes is { Length: > 0 }) return;
        var source = RestoreOrigin(entry.Event, entry.SourceOrigin);
        var completed = ProjectionDescriptors.Single(value => value.SourceEventType == source.GetType()).CompletedEventFactory(source)!;
        entry.CompletionBytes = completed switch
        {
            FuturesEodDataInsertedCompleteEvent e => MessagePack.MessagePackSerializer.Serialize(e),
            VixFuturesEodDataInsertedCompleteEvent e => MessagePack.MessagePackSerializer.Serialize(e),
            _ => throw new InvalidDataException("Unsupported EOD completion event.")
        };
        // Origin sidecars are not part of MessagePack event payloads.
        entry.SourceOrigin ??= TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(source) is { } origin ? TomasAI.IFM.Shared.EventModelActor.MessageOriginBinary.Serialize(origin) : null;
        entry.CompletionOrigin = TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(completed) is { } completionOrigin ? TomasAI.IFM.Shared.EventModelActor.MessageOriginBinary.Serialize(completionOrigin) : entry.SourceOrigin;
    }
    static void PublishCache(EodHistoryEntry entry)
    {
        if (entry.Row is not null) entry.CacheVersion = CurrentFuturesEodCache.Shared.Publish(entry.Row.Snapshot);
        if (entry.Vx is not null) CurrentVixEodCache.Shared.Publish(entry.Vx);
    }
    void SignalWriter() { try { wake.Release(); } catch (SemaphoreFullException) { } }
    async Task AdvanceAsync(IReadOnlyList<EodHistoryEntry> items, int stage)
    {
        await captureGate.WaitAsync().ConfigureAwait(false);
        try { await journal!.AdvanceAsync(items, stage).ConfigureAwait(false); }
        finally { captureGate.Release(); }
    }
    static IEvent RestoreOrigin(IEvent source, string? wire)
    {
        if (wire is not null) TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Restore(source, TomasAI.IFM.Shared.EventModelActor.MessageOriginBinary.Deserialize(wire));
        return source;
    }
    async Task WriteBatchesAsync()
    {
        var lastFlush = journal!.Pending > 0 ? Stopwatch.GetTimestamp() - Stopwatch.Frequency * 60 : Stopwatch.GetTimestamp();
        while (true)
        {
            await wake.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            try
            {
                EodHistoryEntry[] batch;
                await captureGate.WaitAsync().ConfigureAwait(false);
                try { batch = journal!.Batch(512).ToArray(); }
                finally { captureGate.Release(); }
                if (batch.Length == 0) { if (Volatile.Read(ref stopping) != 0) return; continue; }
                var unpublished = batch.Where(item => item.Stage < 1).ToArray();
                foreach (var item in unpublished)
                {
                    await PublishRealtimeEventAsync(Context, RestoreOrigin(item.Event, item.SourceOrigin), ActorName, CancellationToken.None).ConfigureAwait(false);
                }
                await AdvanceAsync(unpublished, 1).ConfigureAwait(false);
                if (batch.Length < 512 && Volatile.Read(ref stopping) == 0 && Stopwatch.GetElapsedTime(lastFlush) < TimeSpan.FromMinutes(1)) continue;
                var unapplied = batch.Where(item => item.Stage < 2).ToArray();
                var rows = unapplied.Where(item => item.Row is not null).Select(item => item.Row!).ToArray();
                foreach (var item in unapplied)
                {
                    if (item.Row is { AppendHistory: true, HistorySequenceId: null } row)
                        row.HistorySequenceId = await sequences.GetSequenceIdAsync(SequenceName.FuturesIntraDay_SequenceId).ConfigureAwait(false);
                    // Freeze allocated IDs before an uncertain database response.

                }
                await AdvanceAsync(unapplied, 1).ConfigureAwait(false);
                if (rows.Length > 0) await dbFactory.MarketDataDb.PersistRealtimeFuturesEodBatchAsync(rows).ConfigureAwait(false);
                var vxRows = unapplied.Where(item => item.Vx is not null).GroupBy(item => (item.Vx!.ContractId, item.Vx.ValueDate)).Select(group => group.Last().Vx!).ToArray();
                if (vxRows.Length > 0) await dbFactory.MarketDataDb.PersistRealtimeVixEodBatchAsync(vxRows).ConfigureAwait(false);
                await AdvanceAsync(unapplied, 2).ConfigureAwait(false);
                foreach (var item in batch)
                {
                    if (item.Stage < 3)
                    {
                        await PublishRealtimeEventAsync(Context, RestoreOrigin(item.Completion, item.CompletionOrigin), ActorName, CancellationToken.None).ConfigureAwait(false);
                        item.Stage = 3;
                    }
                    await captureGate.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        await journal!.CompleteAsync(item).ConfigureAwait(false);
                        if (item.Row is not null && CurrentFuturesEodCache.Shared.TryGet(item.Row.Snapshot.ContractId, item.Row.Snapshot.ValueDate, out var current) && current == item.Row.Snapshot)
                            CurrentFuturesEodCache.Shared.MarkPersisted(item.Row.Snapshot.ContractId, item.Row.Snapshot.ValueDate, item.CacheVersion);
                    }
                    finally { captureGate.Release(); }
                }
                lastFlush = Stopwatch.GetTimestamp();
                if (Volatile.Read(ref stopping) != 0 || journal!.Pending >= 512) SignalWriter();
            }
            catch (Exception error)
            {
                EodHistoryMetrics.Failures.Add(1, new KeyValuePair<string, object?>("stage", "recovery"));
                logger.LogError(error, "EOD durable recovery remains pending");
                if (Volatile.Read(ref stopping) != 0) return;
                await Task.Delay(retryDelay).ConfigureAwait(false);
                if (Volatile.Read(ref stopping) != 0) return;
                SignalWriter();
            }
        }
    }

    readonly ImmutableArray<RealtimeProjectionDescriptor> _descriptors =
    [
        Describe<
            FuturesEodDataInsertedEvent,
            FuturesEodDataInsertedCompleteEvent,
            FuturesEodDataInsertedFailEvent,
            FuturesEodDataId>(e => PersistOneAsync(dbFactory, sequences, e.FuturesEodData)),
        Describe<
            FuturesEodSessionStatisticsUpdatedEvent,
            FuturesEodDataInsertedCompleteEvent,
            FuturesEodDataInsertedFailEvent,
            FuturesEodDataId>(e => PersistOneAsync(dbFactory, sequences, e.FuturesEodData)),
        Describe<
            VixFuturesEodDataInsertedEvent,
            VixFuturesEodDataInsertedCompleteEvent,
            VixFuturesEodDataInsertedFailEvent,
            FuturesEodDataId>(e => dbFactory.MarketDataDb.InsertVixFuturesEodDataAsync(new VixTickObservation(e.VixFuturesTickData,
                e.SessionStatistics)))
    ];

    static async Task PersistOneAsync(IDbContextFactory dbFactory, ISequenceIdGenerator sequences,
        FuturesEodDataV2ReadModel observation)
    {
        var sequenceId = await sequences.GetSequenceIdAsync(SequenceName.FuturesIntraDay_SequenceId).ConfigureAwait(false);
        await dbFactory.MarketDataDb.InsertFuturesEodDataAsync(observation, sequenceId).ConfigureAwait(false);
    }

    public override string ActorName => FuturesEodDataRealtimeActor.ActorName;
    public override string ProjectorName => nameof(FuturesEodDataRealtimeProjector);
    protected override TimeSpan SlowStageLogThreshold => TimeSpan.FromMilliseconds(250);
    public override IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors =>
        _descriptors;
}
