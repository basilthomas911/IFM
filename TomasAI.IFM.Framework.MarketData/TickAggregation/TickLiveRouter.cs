using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.TickAggregation;

/// <summary>
/// Linearizable activation/admission router. Publishing is transient and does
/// not use the tick-aggregation event-source publisher.
/// </summary>
/// <param name="publisher">The destination used to publish tick or option-chain events.</param>
public sealed class TickLiveRouter(ITickLiveEventPublisher publisher) : ITickLiveRouter
{
    private readonly object _sync = new();
    private readonly HashSet<string> _active = new(StringComparer.Ordinal);
    private readonly ITickLiveEventPublisher _publisher =
        publisher ?? throw new ArgumentNullException(nameof(publisher));

    /// <summary>Activates live routing for the specified contract.</summary>
    /// <param name="contractId">The futures or option contract identifier.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool Activate(string contractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        lock (_sync) return _active.Add(contractId);
    }

    /// <summary>Deactivates live routing for the specified contract.</summary>
    /// <param name="contractId">The futures or option contract identifier.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool Deactivate(string contractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        lock (_sync) return _active.Remove(contractId);
    }

    /// <summary>Determines whether live routing is active for the specified contract.</summary>
    /// <param name="contractId">The futures or option contract identifier.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool IsActive(string contractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        lock (_sync) return _active.Contains(contractId);
    }

    /// <summary>Publishes the supplied tick event only when its contract is active.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <returns>An awaitable that completes when the operation finishes.</returns>
    public ValueTask RouteAsync(LiveTickQuoteServiceEvent @event)
        => RouteAsync(@event, CancellationToken.None);

    /// <summary>Publishes the supplied tick event only when its contract is active.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An awaitable that completes when the operation finishes.</returns>
    public ValueTask RouteAsync(
        LiveTickQuoteServiceEvent @event,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return _active.Contains(@event.ContractId)
                ? _publisher.PublishAsync(@event, cancellationToken)
                : ValueTask.CompletedTask;
        }
    }

    /// <summary>Publishes the supplied tick event only when its contract is active.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <returns>An awaitable that completes when the operation finishes.</returns>
    public ValueTask RouteAsync(LiveTickTradeServiceEvent @event)
        => RouteAsync(@event, CancellationToken.None);

    /// <summary>Publishes the supplied tick event only when its contract is active.</summary>
    /// <param name="event">The market data event to publish.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An awaitable that completes when the operation finishes.</returns>
    public ValueTask RouteAsync(
        LiveTickTradeServiceEvent @event,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return _active.Contains(@event.ContractId)
                ? _publisher.PublishAsync(@event, cancellationToken)
                : ValueTask.CompletedTask;
        }
    }

    /// <summary>Clears the registered live routing contracts.</summary>
    public void Clear()
    {
        lock (_sync) _active.Clear();
    }
}
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
