using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Queries current BrokerOrder evidence.</summary>
public sealed class BrokerOrderQueryApi(IActorProducer producer) : NatsClientApi(producer), IBrokerOrderQueryApi
{
    /// <inheritdoc />
    public ValueTask<ServiceResult<BrokerOrderDefinition>> GetAsync(
        BrokerOrderId brokerOrderId,
        CancellationToken cancellationToken = default)
    {
        var subject = new ActorSubject(ActorType.Query, BrokerOrderActorNames.Query,
            GetBrokerOrderQuery.Verb, brokerOrderId.Format());
        return RequestAsync<GetBrokerOrderQuery, BrokerOrderDefinition>(subject,
            new GetBrokerOrderQuery
            {
                Subject = subject,
                EntityId = brokerOrderId,
                BrokerOrderId = brokerOrderId
            }, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<ServiceResult<BrokerOrderDefinition[]>> ListAsync(
        TradeOrderId tradeOrderId,
        CancellationToken cancellationToken = default)
    {
        var subject = new ActorSubject(ActorType.Query, BrokerOrderActorNames.Query,
            GetBrokerOrdersForTradeOrderQuery.Verb, tradeOrderId.Format());
        return RequestAsync<GetBrokerOrdersForTradeOrderQuery, BrokerOrderDefinition[]>(subject,
            new GetBrokerOrdersForTradeOrderQuery
            {
                Subject = subject,
                EntityId = tradeOrderId,
                TradeOrderId = tradeOrderId
            }, cancellationToken);
    }
}
