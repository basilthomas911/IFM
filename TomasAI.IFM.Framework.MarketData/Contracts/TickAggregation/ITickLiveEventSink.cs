using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

public interface ITickLiveEventSink
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask OnQuoteAsync(LiveTickQuoteServiceEvent @event);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask OnQuoteAsync(LiveTickQuoteServiceEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnQuoteAsync(@event);
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask OnTradeAsync(LiveTickTradeServiceEvent @event);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask OnTradeAsync(LiveTickTradeServiceEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnTradeAsync(@event);
    }
}
