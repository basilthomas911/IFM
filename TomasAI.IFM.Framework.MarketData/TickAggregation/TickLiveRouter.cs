using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.TickAggregation;

/// <summary>
/// Linearizable activation/admission router. Publishing is transient and does
/// not use the tick-aggregation event-source publisher.
/// </summary>
public sealed class TickLiveRouter(ITickLiveEventPublisher publisher) : ITickLiveRouter
{
    private readonly object _sync = new();
    private readonly HashSet<string> _active = new(StringComparer.Ordinal);
    private readonly ITickLiveEventPublisher _publisher =
        publisher ?? throw new ArgumentNullException(nameof(publisher));

    public bool Activate(string contractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        lock (_sync) return _active.Add(contractId);
    }

    public bool Deactivate(string contractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        lock (_sync) return _active.Remove(contractId);
    }

    public bool IsActive(string contractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        lock (_sync) return _active.Contains(contractId);
    }

    public ValueTask RouteAsync(LiveTickQuoteServiceEvent @event)
    {
        lock (_sync)
        {
            return _active.Contains(@event.ContractId)
                ? _publisher.PublishAsync(@event)
                : ValueTask.CompletedTask;
        }
    }

    public ValueTask RouteAsync(LiveTickTradeServiceEvent @event)
    {
        lock (_sync)
        {
            return _active.Contains(@event.ContractId)
                ? _publisher.PublishAsync(@event)
                : ValueTask.CompletedTask;
        }
    }

    public void Clear()
    {
        lock (_sync) _active.Clear();
    }
}
public sealed class NullTickLiveEventPublisher : ITickLiveEventPublisher
{
    public ValueTask PublishAsync(LiveTickQuoteServiceEvent @event) =>
        ValueTask.CompletedTask;
    public ValueTask PublishAsync(LiveTickTradeServiceEvent @event) =>
        ValueTask.CompletedTask;
}
