using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

public interface ITickLiveRouter
{
    bool Activate(string contractId);
    bool Deactivate(string contractId);
    bool IsActive(string contractId);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask RouteAsync(LiveTickQuoteServiceEvent @event);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask RouteAsync(LiveTickTradeServiceEvent @event);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask RouteAsync(LiveTickQuoteServiceEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RouteAsync(@event);
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask RouteAsync(LiveTickTradeServiceEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RouteAsync(@event);
    }
    void Clear();
}
