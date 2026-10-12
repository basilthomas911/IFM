using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.TickAggregation;
public sealed class NullTickLiveEventPublisher : ITickLiveEventPublisher
{
    /// <summary>Discards the supplied live event without publishing it; observes an already-cancelled token when provided.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <returns>An already-completed task, or a canceled task when the supplied token is already canceled.</returns>
    public ValueTask PublishAsync(LiveTickQuoteServiceEvent @event) =>
        ValueTask.CompletedTask;
    /// <summary>Discards the supplied live event without publishing it; observes an already-cancelled token when provided.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <returns>An already-completed task, or a canceled task when the supplied token is already canceled.</returns>
    public ValueTask PublishAsync(LiveTickTradeServiceEvent @event) =>
        ValueTask.CompletedTask;
    /// <summary>Discards the supplied live event without publishing it; observes an already-cancelled token when provided.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An already-completed task, or a canceled task when the supplied token is already canceled.</returns>
    public ValueTask PublishAsync(LiveTickQuoteServiceEvent @event, CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled(cancellationToken)
            : ValueTask.CompletedTask;
    /// <summary>Discards the supplied live event without publishing it; observes an already-cancelled token when provided.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An already-completed task, or a canceled task when the supplied token is already canceled.</returns>
    public ValueTask PublishAsync(LiveTickTradeServiceEvent @event, CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled(cancellationToken)
            : ValueTask.CompletedTask;
}
