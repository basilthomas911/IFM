using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

public interface ITickAggregationEventPublisher : IAsyncDisposable
{
    bool IsRunning { get; }
    ValueTask StartAsync();
    ValueTask StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StartAsync();
    }

    /// <summary>Publishes a non-durable normalized market-price update through Core NATS.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent @event);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask PublishAsync(
        FuturesMarketPriceUpdatedRealtimeEvent @event,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return PublishAsync(@event);
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask PublishAsync(FuturesTradeReplayBatchRealtimeEvent @event) =>
        ValueTask.CompletedTask;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask PublishAsync(
        FuturesTradeReplayBatchRealtimeEvent @event,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return PublishAsync(@event);
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask PublishAsync(FuturesSessionStatisticsUpdatedRealtimeEvent @event) =>
        ValueTask.CompletedTask;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask PublishAsync(
        FuturesSessionStatisticsUpdatedRealtimeEvent @event,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return PublishAsync(@event);
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask PublishAsync(FuturesTickTradeDataChangedEvent @event);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask PublishAsync(
        FuturesTickTradeDataChangedEvent @event,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return PublishAsync(@event);
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent @event, ITickQuoteBufferLease lease);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask PublishAsync(
        FuturesTickQuoteDataChangedEvent @event,
        ITickQuoteBufferLease lease,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return PublishAsync(@event, lease);
    }
    ValueTask StopAsync();
    ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StopAsync();
    }
}
