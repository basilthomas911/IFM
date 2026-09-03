using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

public interface ITickLiveEventPublisher
{
    ValueTask PublishAsync(LiveTickQuoteServiceEvent @event);
    ValueTask PublishAsync(LiveTickTradeServiceEvent @event);
    ValueTask PublishAsync(LiveTickQuoteServiceEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return PublishAsync(@event);
    }
    ValueTask PublishAsync(LiveTickTradeServiceEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return PublishAsync(@event);
    }
}

public interface ITickLiveEventSink
{
    ValueTask OnQuoteAsync(LiveTickQuoteServiceEvent @event);
    ValueTask OnTradeAsync(LiveTickTradeServiceEvent @event);
    ValueTask OnQuoteAsync(LiveTickQuoteServiceEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnQuoteAsync(@event);
    }
    ValueTask OnTradeAsync(LiveTickTradeServiceEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnTradeAsync(@event);
    }
}

public interface ITickLiveRouter
{
    bool Activate(string contractId);
    bool Deactivate(string contractId);
    bool IsActive(string contractId);
    ValueTask RouteAsync(LiveTickQuoteServiceEvent @event);
    ValueTask RouteAsync(LiveTickTradeServiceEvent @event);
    ValueTask RouteAsync(LiveTickQuoteServiceEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RouteAsync(@event);
    }
    ValueTask RouteAsync(LiveTickTradeServiceEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RouteAsync(@event);
    }
    void Clear();
}
