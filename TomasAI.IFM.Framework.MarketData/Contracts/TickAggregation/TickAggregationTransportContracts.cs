using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

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
    ValueTask PublishAsync(FuturesTickTradeDataChangedEvent @event);
    ValueTask PublishAsync(FuturesTickQuoteDataChangedEvent @event, ITickQuoteBufferLease lease);
    ValueTask StopAsync();
}
