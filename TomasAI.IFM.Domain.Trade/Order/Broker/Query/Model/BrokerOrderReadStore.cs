using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model;

/// <summary>Provides the immutable hot projection used by broker-order UI and actor queries.</summary>
public interface IBrokerOrderReadStore
{
    /// <summary>Publishes the newest committed state.</summary>
    void Set(BrokerOrderDefinition value);

    /// <summary>Reads one currently projected broker order.</summary>
    bool TryGet(BrokerOrderId id, out BrokerOrderDefinition value);

    /// <summary>Reads every current component order for one accepted Trade Order.</summary>
    BrokerOrderDefinition[] List(TradeOrderId tradeOrderId);
}

/// <summary>Thread-safe immutable-value broker-order projection.</summary>
public sealed class BrokerOrderReadStore : IBrokerOrderReadStore
{
    private readonly ConcurrentDictionary<BrokerOrderId, BrokerOrderDefinition> _orders = [];

    /// <inheritdoc />
    public void Set(BrokerOrderDefinition value) => _orders[value.Id] = value;

    /// <inheritdoc />
    public bool TryGet(BrokerOrderId id, out BrokerOrderDefinition value) =>
        _orders.TryGetValue(id, out value!);

    /// <inheritdoc />
    public BrokerOrderDefinition[] List(TradeOrderId tradeOrderId) =>
        [.. _orders.Values
            .Where(value => value.Id.Execution.TradeOrder == tradeOrderId)
            .OrderBy(value => value.Id.Execution.ExecutionAttemptId)
            .ThenBy(value => value.Id.ComponentId)];
}
