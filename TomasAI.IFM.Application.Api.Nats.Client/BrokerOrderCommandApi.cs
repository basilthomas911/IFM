using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.Api.Nats.Client;

public sealed class BrokerOrderCommandApi(IActorProducer producer) : NatsClientApi(producer), IBrokerOrderCommandApi
{
    public async ValueTask<ServiceResult<Guid>> UpdatePriceAsync(BrokerOrderId id, decimal signedNetDebitLimit, Guid operationId, CancellationToken cancellationToken = default) =>
        await RequestCommandAsync(new RequestBrokerOrderLimitUpdateCommand
        {
            CommandId = operationId, OperationId = operationId, EntityId = id,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command, RequestBrokerOrderLimitUpdateCommand.Verb, id.Format()),
            NewSignedNetDebitLimit = signedNetDebitLimit, EffectiveAtUtc = DateTime.UtcNow
        }, id, cancellationToken);
    public async ValueTask<ServiceResult<Guid>> CancelAsync(BrokerOrderId id, Guid operationId, CancellationToken cancellationToken = default) =>
        await RequestCommandAsync(new RequestBrokerOrderCancelCommand
        {
            CommandId = operationId, OperationId = operationId, EntityId = id,
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command, RequestBrokerOrderCancelCommand.Verb, id.Format()),
            EffectiveAtUtc = DateTime.UtcNow
        }, id, cancellationToken);
}
