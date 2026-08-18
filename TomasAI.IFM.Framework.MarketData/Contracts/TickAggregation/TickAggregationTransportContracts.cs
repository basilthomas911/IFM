using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

public interface ITickQuoteBufferLease : IDisposable
{
    FuturesTickQuoteData[] Buffer { get; }
    ushort Count { get; }
    void SetCount(ushort count);
}

public interface ITickQuoteBufferPool
{
    ITickQuoteBufferLease Rent();
}

public interface ITickAggregationEventPublisher : IAsyncDisposable
{
    bool IsRunning { get; }
    ValueTask StartAsync();

    /// <summary>Publishes a non-durable normalized market-price update through Core NATS.</summary>
    ValueTask PublishAsync(FuturesMarketPriceUpdatedRealtimeEvent @event);
    ValueTask PublishAsync(FuturesSessionStatisticsUpdatedRealtimeEvent @event) =>
        ValueTask.CompletedTask;
    ValueTask PublishAsync(FuturesTickTradeDataChangedEvent @event);
    ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent @event, ITickQuoteBufferLease lease);
    ValueTask StopAsync();
}
