using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

public interface ITickLiveEventPublisher
{
    ValueTask PublishAsync(LiveTickQuoteServiceEvent @event);
    ValueTask PublishAsync(LiveTickTradeServiceEvent @event);
}

public interface ITickLiveEventSink
{
    ValueTask OnQuoteAsync(LiveTickQuoteServiceEvent @event);
    ValueTask OnTradeAsync(LiveTickTradeServiceEvent @event);
}

public interface ITickLiveRouter
{
    bool Activate(string contractId);
    bool Deactivate(string contractId);
    bool IsActive(string contractId);
    ValueTask RouteAsync(LiveTickQuoteServiceEvent @event);
    ValueTask RouteAsync(LiveTickTradeServiceEvent @event);
    void Clear();
}
