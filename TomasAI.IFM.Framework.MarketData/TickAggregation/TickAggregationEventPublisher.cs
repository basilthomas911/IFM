using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.MarketData.TickAggregation;

public sealed class TickAggregationEventPublisher : ITickAggregationEventPublisher, ITickAggregationPublisherDiagnostics
{
    private readonly IActorSupervisor _supervisor;
    private readonly int _capacity;
    private IActorProducer? _realtimeProducer;
    private Channel<Publication>? _channel;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private Task? _worker;
    private int _running;
    private long _pending;
    private readonly BoundedRealtimeTickPublisher? _bounded;

    /// <summary>Creates an ordered realtime publisher with bounded queue admission.</summary>
    /// <param name="supervisor">The actor supervisor providing the realtime producer.</param>
    /// <param name="capacity">The legacy queue capacity; defaults to 1,024 events when no policy is supplied.</param>
    /// <param name="policy">The optional bounded delivery policy; null selects the legacy waiting channel.</param>
    /// <param name="timeProvider">The clock used by bounded delivery; null selects the system clock.</param>
    public TickAggregationEventPublisher(IActorSupervisor supervisor, int capacity = 1024,
        RealtimeTickPublisherPolicy? policy = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(supervisor);
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _supervisor = supervisor;
        _capacity = capacity;
        if (policy is not null)
            _bounded = new BoundedRealtimeTickPublisher(supervisor, policy.Validate(), timeProvider ?? TimeProvider.System);
    }

    public bool IsRunning => _bounded?.IsRunning ?? Volatile.Read(ref _running) != 0;

    /// <summary>Captures the current publisher state and admission diagnostics.</summary>
    /// <returns>The current queue, delivery, and failure diagnostics.</returns>
    public RealtimeTickPublisherSnapshot GetSnapshot() => _bounded?.GetSnapshot()
        ?? new(false, IsRunning, false, false, false, _capacity,
            (int)Math.Min(int.MaxValue, Math.Max(0, Interlocked.Read(ref _pending))), 0,
            TimeSpan.Zero, TimeSpan.Zero, 0, 0, 0, 0, 0, 0, 0, 0,
            RealtimeTickPublisherFailure.None,
            "Live publisher has a bounded, waiting channel; Stage 3 policy is disabled.");

    /// <summary>Resolves the realtime actor producer and starts the publication worker.</summary>
    /// <returns>An awaitable that completes when the operation finishes.</returns>
    public ValueTask StartAsync() => StartAsync(CancellationToken.None);

    /// <summary>Resolves the realtime actor producer and starts the publication worker.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An awaitable that completes when the operation finishes.</returns>
    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        if (_bounded is not null) { await _bounded.StartAsync(cancellationToken).ConfigureAwait(false); return; }
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRunning) return;
            // The primary realtime actor owns this Core NATS producer's lifecycle.
            // Resolve it only after actor-runtime registration has completed.
            _realtimeProducer = _supervisor.GetProducer(new ActorMailboxId(
                ActorType.Realtime,
                FuturesTickTradeDataChangedEvent.Actor));
            _channel = CreateChannel(_capacity);
            Interlocked.Exchange(ref _pending, 0);
            Volatile.Write(ref _running, 1);
            _worker = Task.Run(ProcessAsync);
        }
        finally { _lifecycle.Release(); }
    }

    /// <summary>Admits the supplied event to the publication queue; completion does not imply downstream processing has finished.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <returns>An awaitable that completes when the event is admitted; downstream delivery and processing occur separately.</returns>
    public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent @event)
        => PublishAsync(@event, CancellationToken.None);

    /// <summary>Admits the supplied event to the publication queue; completion does not imply downstream processing has finished.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An awaitable that completes when the event is admitted; downstream delivery and processing occur separately.</returns>
    public ValueTask PublishAsync(
        FuturesTickTradeDataChangedEvent @event,
        CancellationToken cancellationToken)
    {
        if (_bounded is not null) return _bounded.PublishAsync(@event, null, cancellationToken);
        EnsureRunning();
        return EnqueueAsync(
            new Publication(@event, null, cancellationToken), cancellationToken);
    }

    /// <summary>Admits the supplied event to the publication queue; completion does not imply downstream processing has finished.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <returns>An awaitable that completes when the event is admitted; downstream delivery and processing occur separately.</returns>
    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent @event)
        => PublishAsync(@event, CancellationToken.None);

    /// <summary>Admits the supplied event to the publication queue; completion does not imply downstream processing has finished.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An awaitable that completes when the event is admitted; downstream delivery and processing occur separately.</returns>
    public ValueTask PublishAsync(
        FuturesMarketPriceUpdatedRealtimeEvent @event,
        CancellationToken cancellationToken)
    {
        if (_bounded is not null) return _bounded.PublishAsync(@event, null, cancellationToken);
        EnsureRunning();
        ArgumentNullException.ThrowIfNull(@event);
        return EnqueueAsync(
            new Publication(@event, null, cancellationToken), cancellationToken);
    }

    /// <summary>Admits the supplied event to the publication queue; completion does not imply downstream processing has finished.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <returns>An awaitable that completes when the event is admitted; downstream delivery and processing occur separately.</returns>
    public ValueTask PublishAsync(FuturesTradeReplayBatchRealtimeEvent @event) =>
        PublishAsync(@event, CancellationToken.None);

    /// <summary>Admits the supplied event to the publication queue; completion does not imply downstream processing has finished.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An awaitable that completes when the event is admitted; downstream delivery and processing occur separately.</returns>
    public ValueTask PublishAsync(
        FuturesTradeReplayBatchRealtimeEvent @event,
        CancellationToken cancellationToken)
    {
        if (_bounded is not null) return _bounded.PublishAsync(@event, null, cancellationToken);
        EnsureRunning();
        ArgumentNullException.ThrowIfNull(@event);
        return EnqueueAsync(new Publication(@event, null, cancellationToken), cancellationToken);
    }

    /// <summary>Admits the supplied event to the publication queue; completion does not imply downstream processing has finished.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <returns>An awaitable that completes when the event is admitted; downstream delivery and processing occur separately.</returns>
    public ValueTask PublishAsync(FuturesSessionStatisticsUpdatedRealtimeEvent @event)
        => PublishAsync(@event, CancellationToken.None);

    /// <summary>Admits the supplied event to the publication queue; completion does not imply downstream processing has finished.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An awaitable that completes when the event is admitted; downstream delivery and processing occur separately.</returns>
    public ValueTask PublishAsync(
        FuturesSessionStatisticsUpdatedRealtimeEvent @event,
        CancellationToken cancellationToken)
    {
        if (_bounded is not null) return _bounded.PublishAsync(@event, null, cancellationToken);
        EnsureRunning();
        ArgumentNullException.ThrowIfNull(@event);
        return EnqueueAsync(
            new Publication(@event, null, cancellationToken), cancellationToken);
    }

    /// <summary>Admits the supplied event to the publication queue; completion does not imply downstream processing has finished.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <param name="lease">The buffer lease associated with the quote publication.</param>
    /// <returns>An awaitable that completes when the event is admitted; downstream delivery and processing occur separately.</returns>
    public async ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent @event, ITickQuoteBufferLease lease)
        => await PublishAsync(@event, lease, CancellationToken.None).ConfigureAwait(false);

    /// <summary>Admits the supplied event to the publication queue; completion does not imply downstream processing has finished.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <param name="lease">The buffer lease associated with the quote publication.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An awaitable that completes when the event is admitted; downstream delivery and processing occur separately.</returns>
    public async ValueTask PublishAsync(
        FuturesTickQuoteDataChangedEvent @event,
        ITickQuoteBufferLease lease,
        CancellationToken cancellationToken)
    {
        EnsureRunning();
        ArgumentNullException.ThrowIfNull(lease);
        if (!ReferenceEquals(@event.QuoteData.Buffer, lease.Buffer) ||
            @event.QuoteCount != @event.QuoteData.Count ||
            lease.Count != @event.QuoteCount ||
            @event.QuoteCount is 0 or > FuturesTickQuoteDataSegment.MaximumCount)
            throw new ArgumentException("The quote event does not describe the supplied active buffer lease.", nameof(@event));
        if (_bounded is not null)
        {
            await _bounded.PublishAsync(@event, lease, cancellationToken).ConfigureAwait(false);
            return;
        }
        await EnqueueAsync(
            new Publication(@event, lease, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Closes queue admission and waits for the publication worker to stop.</summary>
    /// <returns>An awaitable that completes when the operation finishes.</returns>
    public ValueTask StopAsync() => StopAsync(CancellationToken.None);

    /// <summary>Closes queue admission and waits for the publication worker to stop.</summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An awaitable that completes when the operation finishes.</returns>
    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        if (_bounded is not null) { await _bounded.StopAsync(cancellationToken).ConfigureAwait(false); return; }
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_channel is null) return;
            _channel.Writer.TryComplete();
            Exception? failure = null;
            try
            {
                if (_worker is not null)
                    await _worker.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            _worker = null;
            _channel = null;
            Interlocked.Exchange(ref _pending, 0);
            Volatile.Write(ref _running, 0);
            _realtimeProducer = null;
            if (failure is not null) throw failure;
        }
        finally { _lifecycle.Release(); }
    }

    private async Task ProcessAsync()
    {
        var channel = _channel!;
        try
        {
            await foreach (var publication in channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                Interlocked.Decrement(ref _pending);
                try
                {
                    if (publication.CancellationToken.IsCancellationRequested)
                        continue;
                    switch (publication.Event)
                    {
                        case FuturesTickTradeDataChangedEvent trade:
                            await _realtimeProducer!.SendAsync<FuturesTickTradeDataChangedEvent, TickDataEntityId>(
                                trade.Subject, trade, publication.CancellationToken).ConfigureAwait(false);
                            break;
                        case FuturesTickQuoteDataChangedEvent quote:
                            await _realtimeProducer!.SendAsync<FuturesTickQuoteDataChangedEvent, TickDataEntityId>(
                                quote.Subject, quote, publication.CancellationToken).ConfigureAwait(false);
                            break;
                        case FuturesMarketPriceUpdatedRealtimeEvent price:
                            await _realtimeProducer!.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
                                price.Subject, price, publication.CancellationToken).ConfigureAwait(false);
                            break;
                        case FuturesTradeReplayBatchRealtimeEvent replay:
                            await _realtimeProducer!.SendAsync<FuturesTradeReplayBatchRealtimeEvent, TickDataEntityId>(
                                replay.Subject, replay, publication.CancellationToken).ConfigureAwait(false);
                            break;
                        case FuturesSessionStatisticsUpdatedRealtimeEvent statistics:
                            await _realtimeProducer!.SendAsync<FuturesSessionStatisticsUpdatedRealtimeEvent, FuturesEodDataId>(
                                statistics.Subject, statistics, publication.CancellationToken).ConfigureAwait(false);
                            break;
                    }
                }
                catch (OperationCanceledException) when (publication.CancellationToken.IsCancellationRequested)
                {
                    // A fenced dataset generation must not fault the shared publisher worker.
                }
                finally
                {
                    publication.DisposeLease();
                }
            }
        }
        catch (Exception exception)
        {
            channel.Writer.TryComplete(exception);
            while (channel.Reader.TryRead(out var pending))
            {
                Interlocked.Decrement(ref _pending);
                pending.DisposeLease();
            }
            Volatile.Write(ref _running, 0);
            throw;
        }
    }

    private void EnsureRunning()
    {
        if (!IsRunning) throw new InvalidOperationException("The tick aggregation publisher is not running.");
    }

    private async ValueTask EnqueueAsync(Publication publication, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _pending);
        try
        {
            await _channel!.Writer.WriteAsync(publication, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Decrement(ref _pending);
            throw;
        }
    }

    private static Channel<Publication> CreateChannel(int capacity) =>
        Channel.CreateBounded<Publication>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            // One shared publisher receives ES and VX writes concurrently.
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });

    /// <summary>Asynchronously stops processing and releases the resources owned by this instance.</summary>
    /// <returns>An awaitable that completes when the operation finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_bounded is not null)
        {
            await _bounded.DisposeAsync().ConfigureAwait(false);
            _lifecycle.Dispose();
            return;
        }
        await StopAsync().ConfigureAwait(false);
        _lifecycle.Dispose();
    }

    /// <summary>Initializes a new Publication instance.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <param name="lease">The buffer lease associated with the quote publication.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    private sealed class Publication(
        object @event,
        ITickQuoteBufferLease? lease,
        CancellationToken cancellationToken)
    {
        private ITickQuoteBufferLease? _lease = lease;
        public object Event { get; } = @event;
        public CancellationToken CancellationToken { get; } = cancellationToken;
        /// <summary>Releases the publication&apos;s buffer lease once, preventing duplicate returns to the pool.</summary>
        public void DisposeLease() => Interlocked.Exchange(ref _lease, null)?.Dispose();
    }
}
