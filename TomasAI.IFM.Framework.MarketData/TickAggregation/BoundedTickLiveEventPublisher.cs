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

    /// <summary>Initializes a new BoundedTickLiveEventPublisher instance.</summary>
    /// <param name="sink">The destination receiving admitted live events.</param>
    /// <param name="capacity">The maximum capacity of the buffer, store, or queue.</param>
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

    /// <summary>Admits the supplied event to the publication queue; completion does not imply downstream processing has finished.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <returns>An awaitable that completes when the event is admitted; downstream delivery and processing occur separately.</returns>
    public ValueTask PublishAsync(LiveTickQuoteServiceEvent @event)
        => PublishAsync(@event, CancellationToken.None);

    /// <summary>Admits the supplied event to the publication queue; completion does not imply downstream processing has finished.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An awaitable that completes when the event is admitted; downstream delivery and processing occur separately.</returns>
    public ValueTask PublishAsync(
        LiveTickQuoteServiceEvent @event,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _channel.Writer.WriteAsync(new Publication(@event, null, cancellationToken), cancellationToken);
    }

    /// <summary>Admits the supplied event to the publication queue; completion does not imply downstream processing has finished.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <returns>An awaitable that completes when the event is admitted; downstream delivery and processing occur separately.</returns>
    public ValueTask PublishAsync(LiveTickTradeServiceEvent @event)
        => PublishAsync(@event, CancellationToken.None);

    /// <summary>Admits the supplied event to the publication queue; completion does not imply downstream processing has finished.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An awaitable that completes when the event is admitted; downstream delivery and processing occur separately.</returns>
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

    /// <summary>Asynchronously stops processing and releases the resources owned by this instance.</summary>
    /// <returns>An awaitable that completes when the operation finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _channel.Writer.TryComplete();
        await _worker.ConfigureAwait(false);
    }

    /// <summary>Initializes a new Publication instance.</summary>
    /// <param name="Quote">The bid and ask snapshot, when available.</param>
    /// <param name="Trade">The last trade snapshot, when available.</param>
    /// <param name="CancellationToken">The token used to cancel processing of this publication.</param>
    private readonly record struct Publication(
        LiveTickQuoteServiceEvent? Quote,
        LiveTickTradeServiceEvent? Trade,
        CancellationToken CancellationToken);
}
