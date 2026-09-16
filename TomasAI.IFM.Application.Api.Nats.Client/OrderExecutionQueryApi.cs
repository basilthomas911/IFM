using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Queries durable Trade Order execution evidence.</summary>
public sealed class OrderExecutionQueryApi(IActorProducer producer)
    : NatsClientApi(producer), IOrderExecutionQueryApi
{
    /// <inheritdoc />
    public ValueTask<ServiceResult<OrderExecutionDefinition>> GetAsync(
        TradeOrderId tradeOrderId,
        Guid executionAttemptId,
        CancellationToken cancellationToken = default)
    {
        var id = new OrderExecutionId(tradeOrderId, executionAttemptId);
        var subject = new ActorSubject(ActorType.Query, OrderExecutionActorNames.Query,
            GetOrderExecutionQuery.Verb, id.Format());
        return RequestAsync<GetOrderExecutionQuery, OrderExecutionDefinition>(subject,
            new GetOrderExecutionQuery
            {
                Subject = subject,
                EntityId = id,
                TradeOrderId = tradeOrderId,
                ExecutionAttemptId = executionAttemptId
            }, cancellationToken);
    }
}
