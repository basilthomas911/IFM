using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

public interface ITickLiveEventPublisher
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask PublishAsync(LiveTickQuoteServiceEvent @event);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask PublishAsync(LiveTickTradeServiceEvent @event);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask PublishAsync(LiveTickQuoteServiceEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return PublishAsync(@event);
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing interface named-argument event API used by publishers and sinks.")]
    ValueTask PublishAsync(LiveTickTradeServiceEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return PublishAsync(@event);
    }
}
