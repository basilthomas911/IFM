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
    private IActorProducer? _realtimeProducer;
    private Channel<Publication>? _channel;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private Task? _worker;
    private int _running;
    private readonly BoundedRealtimeTickPublisher? _bounded;

    public TickAggregationEventPublisher(IActorSupervisor supervisor, int capacity = 1024,
        RealtimeTickPublisherPolicy? policy = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(supervisor);
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _supervisor = supervisor;
        if (policy is not null)
            _bounded = new BoundedRealtimeTickPublisher(supervisor, policy.Validate(), timeProvider ?? TimeProvider.System);
    }

    public bool IsRunning => _bounded?.IsRunning ?? Volatile.Read(ref _running) != 0;

    public RealtimeTickPublisherSnapshot GetSnapshot() => _bounded?.GetSnapshot()
        ?? new(false, IsRunning, false, false, false, 0, _channel?.Reader.Count ?? 0, 0,
            TimeSpan.Zero, TimeSpan.Zero, 0, 0, 0, 0, 0, 0, 0, 0,
            RealtimeTickPublisherFailure.None, "Legacy publisher; bounded Stage 3 policy is disabled.");

    public ValueTask StartAsync() => StartAsync(CancellationToken.None);

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
            _channel = CreateChannel();
            Volatile.Write(ref _running, 1);
            _worker = Task.Run(ProcessAsync);
        }
        finally { _lifecycle.Release(); }
    }

    public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent @event)
        => PublishAsync(@event, CancellationToken.None);

    public ValueTask PublishAsync(
        FuturesTickTradeDataChangedEvent @event,
        CancellationToken cancellationToken)
    {
        if (_bounded is not null) return _bounded.PublishAsync(@event, null, cancellationToken);
        EnsureRunning();
        return _channel!.Writer.WriteAsync(
            new Publication(@event, null, cancellationToken), cancellationToken);
    }

    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent @event)
        => PublishAsync(@event, CancellationToken.None);

    public ValueTask PublishAsync(
        FuturesMarketPriceUpdatedRealtimeEvent @event,
        CancellationToken cancellationToken)
    {
        if (_bounded is not null) return _bounded.PublishAsync(@event, null, cancellationToken);
        EnsureRunning();
        ArgumentNullException.ThrowIfNull(@event);
        return _channel!.Writer.WriteAsync(
            new Publication(@event, null, cancellationToken), cancellationToken);
    }

    public ValueTask PublishAsync(FuturesSessionStatisticsUpdatedRealtimeEvent @event)
        => PublishAsync(@event, CancellationToken.None);

    public ValueTask PublishAsync(
        FuturesSessionStatisticsUpdatedRealtimeEvent @event,
        CancellationToken cancellationToken)
    {
        if (_bounded is not null) return _bounded.PublishAsync(@event, null, cancellationToken);
        EnsureRunning();
        ArgumentNullException.ThrowIfNull(@event);
        return _channel!.Writer.WriteAsync(
            new Publication(@event, null, cancellationToken), cancellationToken);
    }

    public async ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent @event, ITickQuoteBufferLease lease)
        => await PublishAsync(@event, lease, CancellationToken.None).ConfigureAwait(false);

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
        await _channel!.Writer.WriteAsync(
            new Publication(@event, lease, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    public ValueTask StopAsync() => StopAsync(CancellationToken.None);

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

    private static Channel<Publication> CreateChannel() =>
        Channel.CreateUnbounded<Publication>(new UnboundedChannelOptions
        {
            SingleReader = true,
            // One shared publisher receives ES and VX writes concurrently.
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

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

    private sealed class Publication(
        object @event,
        ITickQuoteBufferLease? lease,
        CancellationToken cancellationToken)
    {
        private ITickQuoteBufferLease? _lease = lease;
        public object Event { get; } = @event;
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public void DisposeLease() => Interlocked.Exchange(ref _lease, null)?.Dispose();
    }
}
