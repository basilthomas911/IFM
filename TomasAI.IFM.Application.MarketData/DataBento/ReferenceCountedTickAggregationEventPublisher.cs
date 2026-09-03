using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>
/// Shares one epoch publisher across the dataset-specific aggregation services.
/// The underlying transport remains active until the final aggregation releases it.
/// </summary>
internal sealed class ReferenceCountedTickAggregationEventPublisher(
    ITickAggregationEventPublisher inner) : ITickAggregationEventPublisher
{
    private readonly ITickAggregationEventPublisher _inner =
        inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private int _references;
    private int _disposed;

    public bool IsRunning => _inner.IsRunning;

    public ValueTask StartAsync() => StartAsync(CancellationToken.None);

    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_references != 0)
            {
                _references++;
                return;
            }

            await _inner.StartAsync(cancellationToken).ConfigureAwait(false);
            _references = 1;
        }
        finally { _lifecycle.Release(); }
    }

    public ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent @event) =>
        _inner.PublishAsync(@event);

    public ValueTask PublishAsync(
        FuturesMarketPriceUpdatedRealtimeEvent @event,
        CancellationToken cancellationToken) => _inner.PublishAsync(@event, cancellationToken);

    public ValueTask PublishAsync(FuturesSessionStatisticsUpdatedRealtimeEvent @event) =>
        _inner.PublishAsync(@event);

    public ValueTask PublishAsync(
        FuturesSessionStatisticsUpdatedRealtimeEvent @event,
        CancellationToken cancellationToken) => _inner.PublishAsync(@event, cancellationToken);

    public ValueTask PublishAsync(FuturesTickTradeDataChangedEvent @event) =>
        _inner.PublishAsync(@event);

    public ValueTask PublishAsync(
        FuturesTickTradeDataChangedEvent @event,
        CancellationToken cancellationToken) => _inner.PublishAsync(@event, cancellationToken);

    public ValueTask PublishAsync(
        FuturesTickQuoteDataChangedEvent @event,
        ITickQuoteBufferLease lease) => _inner.PublishAsync(@event, lease);

    public ValueTask PublishAsync(
        FuturesTickQuoteDataChangedEvent @event,
        ITickQuoteBufferLease lease,
        CancellationToken cancellationToken) =>
        _inner.PublishAsync(@event, lease, cancellationToken);

    public ValueTask StopAsync() => StopAsync(CancellationToken.None);

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_references == 0) return;
            if (_references > 1)
            {
                _references--;
                return;
            }

            try
            {
                await _inner.StopAsync(cancellationToken).ConfigureAwait(false);
                _references = 0;
            }
            catch
            {
                // Retain the final reference so a later aggregation stop can retry
                // an incomplete transport shutdown.
                _references = 1;
                throw;
            }
        }
        finally { _lifecycle.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_references != 0)
            {
                await _inner.StopAsync().ConfigureAwait(false);
                _references = 0;
            }
        }
        finally
        {
            _lifecycle.Release();
            _lifecycle.Dispose();
        }
    }
}
