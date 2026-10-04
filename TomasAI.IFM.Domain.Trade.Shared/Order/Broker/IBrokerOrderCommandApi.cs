using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

/// <summary>Requests durable broker mutations through the BrokerOrder command actor.</summary>
public interface IBrokerOrderCommandApi
{
    ValueTask<ServiceResult<Guid>> UpdatePriceAsync(BrokerOrderId id, decimal signedNetDebitLimit, Guid operationId, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<Guid>> CancelAsync(BrokerOrderId id, Guid operationId, CancellationToken cancellationToken = default);
}
