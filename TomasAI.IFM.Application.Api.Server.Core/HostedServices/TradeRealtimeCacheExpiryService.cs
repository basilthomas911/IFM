using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Publishes ordered expiry transitions independently per trade, with bounded admission and stable retry identities.</summary>
public sealed class TradeRealtimeCacheExpiryService(TradeRealtimeCacheRegistry cache, IActorProducer producer,
    ILogger<TradeRealtimeCacheExpiryService> logger) : BackgroundService
{
    sealed class Pending(TradeRealtimeSnapshot snapshot)
    {
        public TradeRealtimeSnapshot Snapshot { get; } = snapshot;
        public TradeRealtimeSnapshotChangedEvent Event { get; } = TradeRealtimeSnapshotChangedEvent.From(snapshot);
        public Task<bool>? Attempt { get; set; }
        public long RetryAt { get; set; }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        // At most one transition is queued for each of the registry's 4,096 trades.
        // Busy trades retain their next deadline in the registry; other trades can
        // still expire while a failed publication waits for its bounded retry.
        var pending = new Dictionary<TradeEntityId, Pending>();
        var blocked = new HashSet<TradeEntityId>();
        var owned = new List<Pending>(4);
        var ready = new PriorityQueue<Pending, long>();
        var tick = timer.WaitForNextTickAsync(stoppingToken).AsTask();
        Task? completion = null;
        try
        {
            while (true)
            {
                if (owned.Count == 0) await tick.ConfigureAwait(false);
                else await Task.WhenAny(tick, completion ??= Task.WhenAny(owned.Select(item => item.Attempt!))).ConfigureAwait(false);
                var now = System.Diagnostics.Stopwatch.GetTimestamp();
                foreach (var item in owned.Where(item => item.Attempt!.IsCompleted).ToArray())
                {
                    owned.Remove(item);
                    completion = null;
                    var accepted = await item.Attempt!.ConfigureAwait(false);
                    if (!pending.TryGetValue(item.Snapshot.TradeId, out var current) || !ReferenceEquals(current, item)) continue;
                    if (accepted || !IsCurrent(item)) Remove(item);
                    else
                    {
                        item.Attempt = null;
                        item.RetryAt = now + System.Diagnostics.Stopwatch.Frequency;
                        ready.Enqueue(item, item.RetryAt);
                    }
                }
                if (tick.IsCompleted)
                {
                    if (!await tick.ConfigureAwait(false)) break;
                    tick = timer.WaitForNextTickAsync(stoppingToken).AsTask();
                    foreach (var item in pending.Values.Where(item => !IsCurrent(item)).ToArray()) Remove(item);
                    foreach (var snapshot in cache.Expire(blocked))
                    {
                        var item = new Pending(snapshot);
                        pending.Add(snapshot.TradeId, item);
                        blocked.Add(snapshot.TradeId);
                        ready.Enqueue(item, 0);
                    }
                    if (ready.Count > Math.Max(128, pending.Count * 2))
                    {
                        ready.Clear();
                        foreach (var item in pending.Values.Where(item => item.Attempt is null)) ready.Enqueue(item, item.RetryAt);
                    }
                }
                // Unattempted transitions sort before retries so permanently failed
                // trades cannot monopolize the four dispatch slots. Completion wakes
                // dispatch immediately; healthy bursts are not throttled to 16/sec.
                while (owned.Count < 4 && ready.TryPeek(out _, out var retryAt) && retryAt <= now)
                {
                    var item = ready.Dequeue();
                    if (!pending.TryGetValue(item.Snapshot.TradeId, out var current) || !ReferenceEquals(current, item)) continue;
                    if (!IsCurrent(item)) { Remove(item); continue; }
                    item.Attempt = PublishAsync(item, stoppingToken);
                    owned.Add(item);
                    completion = null;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            try { await Task.WhenAll(owned.Select(item => item.Attempt!)).WaitAsync(TimeSpan.FromSeconds(4)).ConfigureAwait(false); }
            catch (TimeoutException)
            {
                // Keep ownership bounded even if a broken transport ignores cancellation.
                // Shutdown cannot wait indefinitely; observe any eventual task failures.
                foreach (var item in owned)
                    _ = item.Attempt!.ContinueWith(task => _ = task.Exception, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                logger.LogWarning("Trade expiry publication did not stop within the bounded shutdown budget.");
            }
        }

        void Remove(Pending item)
        {
            pending.Remove(item.Snapshot.TradeId);
            blocked.Remove(item.Snapshot.TradeId);
        }
    }

    bool IsCurrent(Pending item) => cache.Find(item.Snapshot.TradeId)?.Read().MonitoringGeneration == item.Snapshot.MonitoringGeneration;

    async Task<bool> PublishAsync(Pending item, CancellationToken stoppingToken)
    {
        if (!IsCurrent(item)) return true;
        try
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            budget.CancelAfter(TimeSpan.FromSeconds(3));
            var changed = item.Event;
            await producer.SendAsync<TradeRealtimeSnapshotChangedEvent, TradeEntityId>(changed.Subject, changed, budget.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return false; }
        catch (Exception error)
        {
            logger.LogError(error, "Trade cache expiry notification will retry for {TradeId}; EventId={EventId}", item.Snapshot.TradeId.Format(), item.Event.Id);
            return false;
        }
    }
}
