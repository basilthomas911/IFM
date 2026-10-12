using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

/// <summary>Reads the current durable state of broker orders.</summary>
public interface IBrokerOrderQueryApi
{
    /// <summary>Gets one broker order by its full durable identity.</summary>
    ValueTask<ServiceResult<BrokerOrderDefinition>> GetAsync(
        BrokerOrderId brokerOrderId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the current logical broker orders for one accepted Trade Order.</summary>
    ValueTask<ServiceResult<BrokerOrderDefinition[]>> ListAsync(
        TradeOrderId tradeOrderId,
        CancellationToken cancellationToken = default);
}
