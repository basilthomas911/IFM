using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.MarketData.TickAggregation;

/// <summary>
/// Opt-in host delivery containment. No raw event is silently coalesced: admission rejects
/// synchronously, and every accepted event discarded after transport failure is accounted for.
/// A stopped/faulted session is never replayed when its replacement starts.
/// </summary>
internal sealed class BoundedRealtimeTickPublisher(
    IActorSupervisor supervisor, RealtimeTickPublisherPolicy policy, TimeProvider time) : IAsyncDisposable
{
    readonly object gate = new();
    readonly SemaphoreSlim lifecycle = new(1, 1);
    Session? session;
    bool faulted;
    bool nonCooperativeLatch;
    bool uncontained;
    RealtimeTickPublisherFailure failure;
    string detail = string.Empty;
    long accepted, published, rejected, saturation, generationCanceled, shutdownDiscarded, expired, failed;

    public bool IsRunning { get { lock (gate) return session?.Accepting == true; } }

    public RealtimeTickPublisherSnapshot GetSnapshot()
    {
        lock (gate)
        {
            var current = session;
            return new(true, current?.Accepting == true, faulted,
                faulted && !nonCooperativeLatch && !uncontained && current?.Worker?.IsCompleted != false,
                uncontained, policy.Capacity, current?.Queue.Count ?? 0, current?.InFlight is null ? 0 : 1,
                current?.Queue.TryPeek(out var oldest) == true ? time.GetElapsedTime(oldest.EnqueuedAt) : TimeSpan.Zero,
                current?.InFlight is { } active ? time.GetElapsedTime(active.EnqueuedAt) : TimeSpan.Zero,
                accepted, published, rejected, saturation, generationCanceled, shutdownDiscarded, expired, failed,
                failure, detail);
        }
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        await lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Session? old;
            lock (gate)
            {
                if (nonCooperativeLatch)
                    throw new RealtimeTickPublisherUnavailableException("a non-cooperative send requires host recovery");
                if (session?.Accepting == true) return;
                old = session;
            }
            if (old?.Worker is { } oldWorker)
                await oldWorker.WaitAsync(policy.SendTimeout + policy.CancellationGracePeriod + TimeSpan.FromSeconds(1),
                    cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var producer = supervisor.GetProducer(new ActorMailboxId(ActorType.Realtime, FuturesTickTradeDataChangedEvent.Actor));
            lock (gate)
            {
                if (nonCooperativeLatch)
                    throw new RealtimeTickPublisherUnavailableException("a non-cooperative send requires host recovery");
                old?.DisposeSignals();
                var replacement = new Session(producer);
                session = replacement;
                faulted = false;
                failure = RealtimeTickPublisherFailure.None;
                detail = string.Empty;
                replacement.Worker = Task.Run(() => ProcessAsync(replacement));
            }
        }
        finally { lifecycle.Release(); }
    }

    /// <summary>Transfers the lease only on successful admission; all rejections leave it with the caller.</summary>
    public ValueTask PublishAsync(object value, ITickQuoteBufferLease? lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        List<Publication>? canceled = null;
        Exception? rejection = null;
        lock (gate)
        {
            var current = session;
            if (current?.Accepting != true)
            {
                rejected++;
                rejection = new RealtimeTickPublisherUnavailableException(detail.Length == 0 ? "not running" : detail);
            }
            else
            {
                // Retired generations need not occupy admission capacity behind an unrelated slow send.
                if (current.Queue.Count >= policy.Capacity)
                {
                    var count = current.Queue.Count;
                    for (var index = 0; index < count; index++)
                    {
                        var pending = current.Queue.Dequeue();
                        if (pending.Token.IsCancellationRequested)
                        {
                            generationCanceled++;
                            (canceled ??= []).Add(pending);
                        }
                        else current.Queue.Enqueue(pending);
                    }
                }
                if (current.Queue.Count >= policy.Capacity)
                {
                    rejected++;
                    saturation++;
                    failure = RealtimeTickPublisherFailure.Saturated;
                    detail = "A raw realtime publication was rejected because the bounded queue was full.";
                    rejection = new RealtimeTickPublisherSaturatedException(policy.Capacity);
                }
                else
                {
                    current.Queue.Enqueue(new Publication(value, lease, cancellationToken, time.GetTimestamp()));
                    accepted++;
                    // A binary wake-up cannot accumulate phantom permits as retired generations
                    // are pruned. Queue access and signaling share the same gate.
                    if (current.Available.CurrentCount == 0) current.Available.Release();
                }
            }
        }
        if (canceled is not null) foreach (var item in canceled) item.DisposeLease();
        if (rejection is not null) throw rejection;
        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        await lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Session? current;
            List<Publication> pending;
            lock (gate)
            {
                current = session;
                if (current is null) return;
                current.Accepting = false;
                pending = Drain(current, onStop: true);
            }
            Cancel(current.Stopping);
            foreach (var item in pending) item.DisposeLease();
            if (current.Worker is { } worker)
                await worker.WaitAsync(policy.SendTimeout + policy.CancellationGracePeriod + TimeSpan.FromSeconds(1),
                    cancellationToken).ConfigureAwait(false);
        }
        finally { lifecycle.Release(); }
    }

    async Task ProcessAsync(Session current)
    {
        while (true)
        {
            try { await current.Available.WaitAsync(current.Stopping.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (current.Stopping.IsCancellationRequested) { return; }
            Publication? item;
            lock (gate)
            {
                if (!current.Queue.TryDequeue(out item))
                {
                    if (!current.Accepting) return;
                    continue;
                }
                current.InFlight = item;
                if (current.Queue.Count > 0 && current.Available.CurrentCount == 0)
                    current.Available.Release();
            }
            var retainLease = false;
            try
            {
                if (item.Token.IsCancellationRequested)
                {
                    lock (gate) generationCanceled++;
                    continue;
                }
                if (current.Stopping.IsCancellationRequested)
                {
                    lock (gate) shutdownDiscarded++;
                    continue;
                }
                if (time.GetElapsedTime(item.EnqueuedAt) > policy.MaximumQueueAge)
                {
                    lock (gate) expired++;
                    Fault(current, RealtimeTickPublisherFailure.QueueExpired,
                        "Queued realtime data exceeded its maximum age; the outage backlog was discarded.");
                    return;
                }

                using var deadline = new CancellationTokenSource(policy.SendTimeout, time);
                using var stopping = CancellationTokenSource.CreateLinkedTokenSource(
                    item.Token, current.Stopping.Token, deadline.Token);
                var sendToken = stopping.Token;
                // Isolate even a producer that blocks synchronously before returning its ValueTask.
                // Only one such invocation is permitted; timeout never starts an overlapping sender.
                var sending = Task.Run(async () =>
                {
                    sendToken.ThrowIfCancellationRequested();
                    await SendAsync(current.Producer, item.Value, sendToken).ConfigureAwait(false);
                });
                try
                {
                    await sending.WaitAsync(policy.SendTimeout + policy.CancellationGracePeriod, time)
                        .ConfigureAwait(false);
                    if (deadline.IsCancellationRequested)
                    {
                        lock (gate) failed++;
                        Fault(current, RealtimeTickPublisherFailure.SendTimedOut,
                            "The transport completed after its delivery deadline; queued output was discarded.");
                        return;
                    }
                    lock (gate) published++;
                }
                catch (TimeoutException)
                {
                    lock (gate)
                    {
                        failed++;
                        nonCooperativeLatch = true;
                        uncontained = true;
                    }
                    retainLease = true;
                    Fault(current, RealtimeTickPublisherFailure.NonCooperativeSend,
                        "The transport did not stop after cancellation; its in-flight lease is retained and host recovery is required.");
                    _ = RetireUncontainedAsync(current, item, sending);
                    return;
                }
                catch (OperationCanceledException) when (item.Token.IsCancellationRequested)
                {
                    lock (gate) generationCanceled++;
                }
                catch (OperationCanceledException) when (current.Stopping.IsCancellationRequested)
                {
                    lock (gate) shutdownDiscarded++;
                }
                catch (Exception exception)
                {
                    lock (gate) failed++;
                    Fault(current, deadline.IsCancellationRequested
                            ? RealtimeTickPublisherFailure.SendTimedOut : RealtimeTickPublisherFailure.TransportFailed,
                        $"Realtime delivery failed; queued output was discarded: {exception.GetType().Name}.");
                    return;
                }
            }
            finally
            {
                if (!retainLease)
                {
                    item.DisposeLease();
                    lock (gate) if (ReferenceEquals(current.InFlight, item)) current.InFlight = null;
                }
            }
        }
    }

    async Task RetireUncontainedAsync(Session current, Publication item, Task sending)
    {
        try { await sending.ConfigureAwait(false); }
        catch (Exception) { /* The fault has already been recorded and the session fenced. */ }
        finally
        {
            item.DisposeLease();
            lock (gate)
            {
                if (ReferenceEquals(current.InFlight, item)) current.InFlight = null;
                uncontained = false;
            }
        }
    }

    void Fault(Session current, RealtimeTickPublisherFailure reason, string message)
    {
        List<Publication> pending;
        lock (gate)
        {
            current.Accepting = false;
            faulted = true;
            failure = reason;
            detail = message;
            pending = Drain(current, onStop: false);
        }
        Cancel(current.Stopping);
        foreach (var item in pending) item.DisposeLease();
    }

    List<Publication> Drain(Session current, bool onStop)
    {
        var result = new List<Publication>(current.Queue.Count);
        while (current.Queue.TryDequeue(out var item))
        {
            if (item.Token.IsCancellationRequested) generationCanceled++;
            else if (onStop) shutdownDiscarded++;
            else if (time.GetElapsedTime(item.EnqueuedAt) > policy.MaximumQueueAge) expired++;
            else failed++;
            result.Add(item);
        }
        return result;
    }

    static void Cancel(CancellationTokenSource source) =>
        _ = source.CancelAsync().ContinueWith(task => _ = task.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

    static ValueTask SendAsync(IActorProducer producer, object value, CancellationToken token) => value switch
    {
        FuturesTickTradeDataChangedEvent trade => producer.SendAsync<FuturesTickTradeDataChangedEvent, TickDataEntityId>(trade.Subject, trade, token),
        FuturesTickQuoteDataChangedEvent quote => producer.SendAsync<FuturesTickQuoteDataChangedEvent, TickDataEntityId>(quote.Subject, quote, token),
        FuturesMarketPriceUpdatedRealtimeEvent price => producer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(price.Subject, price, token),
        FuturesSessionStatisticsUpdatedRealtimeEvent statistics => producer.SendAsync<FuturesSessionStatisticsUpdatedRealtimeEvent, FuturesEodDataId>(statistics.Subject, statistics, token),
        _ => throw new ArgumentException("Unsupported realtime publication.", nameof(value))
    };

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        lock (gate) session?.DisposeSignals();
        lifecycle.Dispose();
    }

    sealed class Session(IActorProducer producer)
    {
        public IActorProducer Producer { get; } = producer;
        public Queue<Publication> Queue { get; } = new();
        public SemaphoreSlim Available { get; } = new(0, 1);
        public CancellationTokenSource Stopping { get; } = new();
        public Task? Worker;
        public Publication? InFlight;
        public bool Accepting = true;
        public void DisposeSignals() { Available.Dispose(); Stopping.Dispose(); }
    }

    sealed class Publication(object value, ITickQuoteBufferLease? lease, CancellationToken token, long enqueuedAt)
    {
        ITickQuoteBufferLease? ownedLease = lease;
        public object Value { get; } = value;
        public CancellationToken Token { get; } = token;
        public long EnqueuedAt { get; } = enqueuedAt;
        public void DisposeLease() => Interlocked.Exchange(ref ownedLease, null)?.Dispose();
    }
}
