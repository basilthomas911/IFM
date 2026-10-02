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
    DateTime? lastAcceptedUtc, lastPublishedUtc, firstFailureUtc, lastFailureUtc;
    string lastExceptionType = string.Empty, lastExceptionMessage = string.Empty;
    long accepted, published, rejected, saturation, generationCanceled, shutdownDiscarded, expired, failed;

    public bool IsRunning { get { lock (gate) return session?.Accepting == true; } }

    public RealtimeTickPublisherSnapshot GetSnapshot()
    {
        lock (gate)
        {
            var current = session;
            var noProgressSince = current?.InFlight?.EnqueuedUtc
                ?? (current?.Queue.TryPeek(out var pending) == true ? pending.EnqueuedUtc : (DateTime?)null)
                ?? (faulted ? firstFailureUtc : null);
            var noProgressAge = noProgressSince is { } since
                ? time.GetUtcNow().UtcDateTime - (lastPublishedUtc > since ? lastPublishedUtc.Value : since)
                : TimeSpan.Zero;
            return new(true, current?.Accepting == true, faulted,
                faulted && !nonCooperativeLatch && !uncontained && current?.Worker?.IsCompleted != false,
                uncontained, policy.Capacity, current?.Queue.Count ?? 0, current?.InFlight is null ? 0 : 1,
                current?.Queue.TryPeek(out var oldest) == true ? time.GetElapsedTime(oldest.EnqueuedAt) : TimeSpan.Zero,
                current?.InFlight is { } active ? time.GetElapsedTime(active.EnqueuedAt) : TimeSpan.Zero,
                accepted, published, rejected, saturation, generationCanceled, shutdownDiscarded, expired, failed,
                failure, detail)
            {
                RetainedQuoteItems = current?.RetainedQuoteItems ?? 0,
                MaximumRetainedQuoteItems = policy.MaximumQueuedQuoteItems,
                LastAcceptedUtc = lastAcceptedUtc,
                LastPublishedUtc = lastPublishedUtc,
                FirstFailureUtc = firstFailureUtc,
                LastFailureUtc = lastFailureUtc,
                LastExceptionType = lastExceptionType,
                LastExceptionMessage = lastExceptionMessage,
                InFlightEventType = current?.InFlight?.Value.GetType().FullName ?? string.Empty,
                InFlightSubject = current?.InFlight is { } inflight ? Subject(inflight.Value) : string.Empty,
                CurrentAttempt = current?.CurrentAttempt ?? 0,
                NoProgressAge = noProgressAge < TimeSpan.Zero ? TimeSpan.Zero : noProgressAge,
                NoProgressResetThreshold = policy.NoProgressResetThreshold,
                ResetRequired = (current?.InFlight is not null || current?.Queue.Count > 0 || faulted)
                    && noProgressAge >= policy.NoProgressResetThreshold
            };
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
                    throw new RealtimeTickPublisherUnavailableException(
                        "a non-cooperative send cannot prove downstream generation isolation; restart the host");
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
                    throw new RealtimeTickPublisherUnavailableException(
                        "a non-cooperative send cannot prove downstream generation isolation; restart the host");
                old?.DisposeSignals();
                var replacement = new Session(producer);
                session = replacement;
                faulted = false;
                failure = RealtimeTickPublisherFailure.None;
                detail = string.Empty;
                firstFailureUtc = null;
                lastFailureUtc = null;
                lastExceptionType = string.Empty;
                lastExceptionMessage = string.Empty;
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
        var quoteItems = value is FuturesTickQuoteDataChangedEvent quote ? quote.QuoteCount : 0;
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
                if (current.Queue.Count >= policy.Capacity
                    || quoteItems > policy.MaximumQueuedQuoteItems - current.RetainedQuoteItems)
                {
                    var count = current.Queue.Count;
                    for (var index = 0; index < count; index++)
                    {
                        var pending = current.Queue.Dequeue();
                        if (pending.Token.IsCancellationRequested)
                        {
                            generationCanceled++;
                            current.RetainedQuoteItems -= pending.QuoteItems;
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
                else if (quoteItems > policy.MaximumQueuedQuoteItems - current.RetainedQuoteItems)
                {
                    rejected++;
                    saturation++;
                    failure = RealtimeTickPublisherFailure.Saturated;
                    detail = "A quote publication was rejected because the retained quote-item limit was reached.";
                    rejection = new RealtimeTickPublisherQuoteBudgetExceededException(
                        policy.MaximumQueuedQuoteItems);
                }
                else
                {
                    current.Queue.Enqueue(new Publication(
                        value, lease, cancellationToken, time.GetTimestamp(), time.GetUtcNow().UtcDateTime, quoteItems));
                    current.RetainedQuoteItems += quoteItems;
                    accepted++;
                    lastAcceptedUtc = time.GetUtcNow().UtcDateTime;
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

                for (var attempt = 1; attempt <= policy.MaximumRetryAttempts + 1; attempt++)
                {
                    lock (gate) current.CurrentAttempt = attempt;
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
                            throw new TimeoutException();
                        }
                        lock (gate)
                        {
                            published++;
                            lastPublishedUtc = time.GetUtcNow().UtcDateTime;
                            current.CurrentAttempt = 0;
                        }
                        break;
                    }
                    catch (TimeoutException exception) when (!sending.IsCompleted)
                    {
                        RecordFailure(exception);
                        lock (gate)
                        {
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
                        break;
                    }
                    catch (OperationCanceledException) when (current.Stopping.IsCancellationRequested)
                    {
                        lock (gate) shutdownDiscarded++;
                        break;
                    }
                    catch (Exception exception)
                    {
                        RecordFailure(exception);
                        if (attempt == policy.MaximumRetryAttempts + 1
                            || time.GetUtcNow().UtcDateTime - item.EnqueuedUtc >= policy.NoProgressResetThreshold)
                        {
                            Fault(current, deadline.IsCancellationRequested
                                    ? RealtimeTickPublisherFailure.SendTimedOut : RealtimeTickPublisherFailure.TransportFailed,
                                $"Realtime delivery failed after {attempt} attempt(s): {exception.GetType().Name}: {exception.Message}");
                            return;
                        }
                        var delay = RetryDelay(attempt);
                        var remaining = policy.NoProgressResetThreshold
                            - (time.GetUtcNow().UtcDateTime - item.EnqueuedUtc);
                        if (remaining <= TimeSpan.Zero)
                        {
                            Fault(current, RealtimeTickPublisherFailure.TransportFailed,
                                $"Realtime delivery made no progress for {policy.NoProgressResetThreshold.TotalSeconds:F0} seconds.");
                            return;
                        }
                        if (delay > remaining) delay = remaining;
                        try { await Task.Delay(delay, time, current.Stopping.Token).ConfigureAwait(false); }
                        catch (OperationCanceledException) when (current.Stopping.IsCancellationRequested) { break; }
                    }
                }
            }
            finally
            {
                if (!retainLease)
                {
                    item.DisposeLease();
                    lock (gate)
                    {
                        if (ReferenceEquals(current.InFlight, item))
                        {
                            current.InFlight = null;
                            current.RetainedQuoteItems -= item.QuoteItems;
                        }
                    }
                }
            }
        }
    }

    async Task RetireUncontainedAsync(Session current, Publication item, Task sending)
    {
        try { await sending.ConfigureAwait(false); }
        catch (Exception exception) { RecordFailure(exception, increment: false); }
        finally
        {
            item.DisposeLease();
            lock (gate)
            {
                if (ReferenceEquals(current.InFlight, item))
                {
                    current.InFlight = null;
                    current.RetainedQuoteItems -= item.QuoteItems;
                }
                uncontained = false;
            }
        }
    }

    void RecordFailure(Exception exception, bool increment = true)
    {
        lock (gate)
        {
            if (increment) failed++;
            var now = time.GetUtcNow().UtcDateTime;
            firstFailureUtc ??= now;
            lastFailureUtc = now;
            lastExceptionType = exception.GetType().FullName ?? exception.GetType().Name;
            lastExceptionMessage = exception.Message;
        }
    }

    TimeSpan RetryDelay(int failedAttempt)
    {
        var numerator = failedAttempt switch
        {
            <= 1 => 2L,
            2 => 5L,
            3 => 10L,
            4 => 20L,
            _ => 40L
        };
        var ticks = Math.Min(policy.MaximumRetryDelay.Ticks,
            checked(policy.InitialRetryDelay.Ticks * numerator / 2));
        return TimeSpan.FromTicks(ticks);
    }

    static string Subject(object value) => value switch
    {
        FuturesTickTradeDataChangedEvent trade => trade.Subject.ToString(),
        FuturesTickQuoteDataChangedEvent quote => quote.Subject.ToString(),
        FuturesMarketPriceUpdatedRealtimeEvent price => price.Subject.ToString(),
        FuturesTradeReplayBatchRealtimeEvent replay => replay.Subject.ToString(),
        FuturesSessionStatisticsUpdatedRealtimeEvent statistics => statistics.Subject.ToString(),
        _ => string.Empty
    };

    void Fault(Session current, RealtimeTickPublisherFailure reason, string message)
    {
        List<Publication> pending;
        lock (gate)
        {
            current.Accepting = false;
            faulted = true;
            failure = reason;
            detail = message;
            var now = time.GetUtcNow().UtcDateTime;
            firstFailureUtc ??= now;
            lastFailureUtc = now;
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
            current.RetainedQuoteItems -= item.QuoteItems;
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
        FuturesTradeReplayBatchRealtimeEvent replay => producer.SendAsync<FuturesTradeReplayBatchRealtimeEvent, TickDataEntityId>(replay.Subject, replay, token),
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
        public int RetainedQuoteItems;
        public int CurrentAttempt;
        public bool Accepting = true;
        public void DisposeSignals() { Available.Dispose(); Stopping.Dispose(); }
    }

    sealed class Publication(
        object value, ITickQuoteBufferLease? lease, CancellationToken token, long enqueuedAt,
        DateTime enqueuedUtc, int quoteItems)
    {
        ITickQuoteBufferLease? ownedLease = lease;
        public object Value { get; } = value;
        public CancellationToken Token { get; } = token;
        public long EnqueuedAt { get; } = enqueuedAt;
        public DateTime EnqueuedUtc { get; } = enqueuedUtc;
        public int QuoteItems { get; } = quoteItems;
        public void DisposeLease() => Interlocked.Exchange(ref ownedLease, null)?.Dispose();
    }
}
