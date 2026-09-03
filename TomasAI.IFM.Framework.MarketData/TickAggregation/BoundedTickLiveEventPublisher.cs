using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.TickAggregation;

/// <summary>
/// Bounded, ordered transient publisher for an event-router/UI/strategy sink.
/// It has no event-source or storage dependency.
/// </summary>
public sealed class BoundedTickLiveEventPublisher :
    ITickLiveEventPublisher,
    IAsyncDisposable
{
    private readonly ITickLiveEventSink _sink;
    private readonly Channel<Publication> _channel;
    private readonly Task _worker;
    private int _disposed;

    public BoundedTickLiveEventPublisher(ITickLiveEventSink sink, int capacity = 1024)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _channel = Channel.CreateBounded<Publication>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
        _worker = Task.Run(ProcessAsync);
    }

    public ValueTask PublishAsync(LiveTickQuoteServiceEvent @event)
        => PublishAsync(@event, CancellationToken.None);

    public ValueTask PublishAsync(
        LiveTickQuoteServiceEvent @event,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _channel.Writer.WriteAsync(new Publication(@event, null, cancellationToken), cancellationToken);
    }

    public ValueTask PublishAsync(LiveTickTradeServiceEvent @event)
        => PublishAsync(@event, CancellationToken.None);

    public ValueTask PublishAsync(
        LiveTickTradeServiceEvent @event,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _channel.Writer.WriteAsync(new Publication(null, @event, cancellationToken), cancellationToken);
    }

    private async Task ProcessAsync()
    {
        await foreach (var publication in _channel.Reader.ReadAllAsync()
            .ConfigureAwait(false))
        {
            if (publication.CancellationToken.IsCancellationRequested)
                continue;
            if (publication.Quote is { } quote)
                await _sink.OnQuoteAsync(quote, publication.CancellationToken).ConfigureAwait(false);
            else if (publication.Trade is { } trade)
                await _sink.OnTradeAsync(trade, publication.CancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _channel.Writer.TryComplete();
        await _worker.ConfigureAwait(false);
    }

    private readonly record struct Publication(
        LiveTickQuoteServiceEvent? Quote,
        LiveTickTradeServiceEvent? Trade,
        CancellationToken CancellationToken);
}
