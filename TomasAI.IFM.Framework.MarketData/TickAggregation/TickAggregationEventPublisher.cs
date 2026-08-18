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

public sealed class TickAggregationEventPublisher : ITickAggregationEventPublisher
{
    private readonly IActorSupervisor _supervisor;
    private IActorProducer? _realtimeProducer;
    private readonly int _capacity;
    private Channel<Publication>? _channel;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private Task? _worker;
    private int _running;

    public TickAggregationEventPublisher(IActorSupervisor supervisor, int capacity = 1024)
    {
        ArgumentNullException.ThrowIfNull(supervisor);
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _supervisor = supervisor;
        _capacity = capacity;
    }

    public bool IsRunning => Volatile.Read(ref _running) != 0;

    public async ValueTask StartAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
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
    {
        EnsureRunning();
        return _channel!.Writer.WriteAsync(new Publication(@event, null));
    }

    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent @event)
    {
        EnsureRunning();
        ArgumentNullException.ThrowIfNull(@event);
        return (_realtimeProducer ?? throw new InvalidOperationException(
                "The tick aggregation publisher is not running."))
            .SendAsync<
            FuturesMarketPriceUpdatedRealtimeEvent,
            TickDataEntityId>(@event.Subject, @event);
    }

    public ValueTask PublishAsync(FuturesSessionStatisticsUpdatedRealtimeEvent @event)
    {
        EnsureRunning();
        ArgumentNullException.ThrowIfNull(@event);
        return (_realtimeProducer ?? throw new InvalidOperationException(
                "The tick aggregation publisher is not running."))
            .SendAsync<
                FuturesSessionStatisticsUpdatedRealtimeEvent,
                FuturesEodDataId>(@event.Subject, @event);
    }

    public async ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent @event, ITickQuoteBufferLease lease)
    {
        EnsureRunning();
        ArgumentNullException.ThrowIfNull(lease);
        if (!ReferenceEquals(@event.QuoteData.Buffer, lease.Buffer) ||
            @event.QuoteCount != @event.QuoteData.Count ||
            lease.Count != @event.QuoteCount ||
            @event.QuoteCount is 0 or > FuturesTickQuoteDataSegment.MaximumCount)
            throw new ArgumentException("The quote event does not describe the supplied active buffer lease.", nameof(@event));
        await _channel!.Writer.WriteAsync(new Publication(@event, lease)).ConfigureAwait(false);
    }

    public async ValueTask StopAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_channel is null) return;
            _channel.Writer.TryComplete();
            Exception? failure = null;
            try
            {
                if (_worker is not null) await _worker.ConfigureAwait(false);
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
                    switch (publication.Event)
                    {
                        case FuturesTickTradeDataChangedEvent trade:
                            await _realtimeProducer!.SendAsync<FuturesTickTradeDataChangedEvent, TickDataEntityId>(trade.Subject, trade).ConfigureAwait(false);
                            break;
                        case FuturesTickQuoteDataChangedEvent quote:
                            await _realtimeProducer!.SendAsync<FuturesTickQuoteDataChangedEvent, TickDataEntityId>(quote.Subject, quote).ConfigureAwait(false);
                            break;
                    }
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

    private Channel<Publication> CreateChannel() =>
        Channel.CreateBounded<Publication>(new BoundedChannelOptions(_capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _lifecycle.Dispose();
    }

    private sealed class Publication(object @event, ITickQuoteBufferLease? lease)
    {
        private ITickQuoteBufferLease? _lease = lease;
        public object Event { get; } = @event;
        public void DisposeLease() => Interlocked.Exchange(ref _lease, null)?.Dispose();
    }
}
