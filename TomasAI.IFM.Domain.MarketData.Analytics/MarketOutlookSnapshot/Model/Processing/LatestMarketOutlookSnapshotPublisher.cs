using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;

/// <summary>
/// Publishes the newest Market Outlook to realtime consumers immediately and periodically replaces
/// the single durable row used to hydrate the next process. Intermediate display snapshots are
/// intentionally coalesced and are never replayed.
/// </summary>
public sealed class LatestMarketOutlookSnapshotPublisher(
    IDbContextFactory dbFactory,
    IActorSupervisor supervisor,
    MarketOutlookSnapshotPersistencePolicy persistencePolicy,
    ILogger<LatestMarketOutlookSnapshotPublisher> logger)
    : BackgroundService, IMarketOutlookSnapshotPublisher
{
    sealed record PendingSnapshot(MarketOutlookReadModel Snapshot, long Sequence);

    readonly object gate = new();
    readonly Dictionary<MarketOutlookEntityId, PendingSnapshot> pending = [];
    readonly SemaphoreSlim flushGate = new(1, 1);
    IActorProducer? producer;
    long sequence;

    internal int PendingCount
    {
        get
        {
            lock (gate)
                return pending.Count;
        }
    }

    public async ValueTask PublishAsync(
        MarketOutlookUpdate update,
        MarketOutlookReadModel snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        var latest = snapshot with { SnapshotSource = persistencePolicy.SnapshotSource };
        QueueLatest(update.EntityId, latest);

        var commandId = update.UpdateId == Guid.Empty ? Guid.NewGuid() : update.UpdateId;
        var notification = new MarketOutlookSnapshotInsertedEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                MarketOutlookSnapshotInsertedEvent.Actor,
                MarketOutlookSnapshotInsertedEvent.Verb,
                update.EntityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = update.EntityId,
            CommandId = commandId,
            AggregateId = string.IsNullOrWhiteSpace(update.AggregateId)
                ? update.EntityId.Format()
                : update.AggregateId,
            EventSource = string.IsNullOrWhiteSpace(update.EventSource)
                ? nameof(LatestMarketOutlookSnapshotPublisher)
                : update.EventSource,
            ReceivedOn = DateTime.UtcNow,
            MarketOutlook = latest
        };
        if (!notification.IsValid)
            throw new InvalidOperationException(
                $"Latest Market Outlook notification for {update.EntityId.Format()} is invalid.");

        producer ??= supervisor.GetProducer(notification.Subject.ActorId);
        await producer.SendAsync<MarketOutlookSnapshotInsertedEvent, MarketOutlookEntityId>(
            notification.Subject,
            notification,
            cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = persistencePolicy.PersistenceInterval;
        if (interval <= TimeSpan.Zero)
            throw new InvalidOperationException("Market Outlook persistence interval must be positive.");

        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await FlushPendingAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown. StopAsync performs one final bounded flush.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        using var finalFlush = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        finalFlush.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await FlushPendingAsync(finalFlush.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (finalFlush.IsCancellationRequested)
        {
            logger.LogWarning(
                "Latest Market Outlook persistence did not complete within the shutdown deadline; {PendingCount} latest rows remain",
                PendingCount);
        }
    }

    internal async Task FlushPendingAsync(CancellationToken cancellationToken = default)
    {
        await flushGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PendingSnapshot[] batch;
            lock (gate)
            {
                batch = pending.Values.ToArray();
                pending.Clear();
            }

            foreach (var item in batch)
            {
                try
                {
                    await dbFactory.MarketDataDb.UpsertMarketOutlookSnapshotAsync(
                        item.Snapshot,
                        item.Sequence,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Requeue(item);
                    throw;
                }
                catch (Exception exception)
                {
                    Requeue(item);
                    logger.LogError(
                        exception,
                        "Unable to persist latest Market Outlook snapshot for {ContractId}/{ValueDate}; the newest value will be retried",
                        item.Snapshot.ContractId,
                        item.Snapshot.ValueDate);
                }
            }
        }
        finally
        {
            flushGate.Release();
        }
    }

    void QueueLatest(MarketOutlookEntityId entityId, MarketOutlookReadModel snapshot)
    {
        var item = new PendingSnapshot(snapshot, Interlocked.Increment(ref sequence));
        lock (gate)
        {
            if (!pending.TryGetValue(entityId, out var existing)
                || snapshot.UpdatedAtUtc >= existing.Snapshot.UpdatedAtUtc)
                pending[entityId] = item;
        }
    }

    void Requeue(PendingSnapshot item)
    {
        var entityId = new MarketOutlookEntityId(item.Snapshot.ContractId, item.Snapshot.ValueDate);
        lock (gate)
        {
            if (!pending.TryGetValue(entityId, out var existing)
                || item.Sequence > existing.Sequence)
                pending[entityId] = item;
        }
    }
}
