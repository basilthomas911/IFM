using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model;
/// <summary>Provides the immutable hot projection used by broker-order UI and actor queries.</summary>
public interface IBrokerOrderReadStore
{
    /// <summary>Publishes the newest committed state.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public interface member used by providers and actor callers.")]
    void Set(BrokerOrderDefinition value);
    /// <summary>Reads one currently projected broker order.</summary>
    bool TryGet(BrokerOrderId id, out BrokerOrderDefinition value);
    /// <summary>Reads every current component order for one accepted Trade Order.</summary>
    BrokerOrderDefinition[] List(TradeOrderId tradeOrderId);
}
