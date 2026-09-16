using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Queries durable broker-account qualification and gate state.</summary>
public sealed class BrokerAccountQueryApi(IActorProducer producer) : NatsClientApi(producer), IBrokerAccountQueryApi
{
    /// <inheritdoc />
    public ValueTask<ServiceResult<BrokerAccountDefinition>> GetAsync(
        BrokerAccountId accountId,
        CancellationToken cancellationToken = default)
    {
        var subject = new ActorSubject(ActorType.Query, BrokerAccountActorNames.Query,
            GetBrokerAccountQuery.Verb, accountId.Format());
        return RequestAsync<GetBrokerAccountQuery, BrokerAccountDefinition>(subject,
            new GetBrokerAccountQuery
            {
                Subject = subject,
                EntityId = accountId,
                BrokerAccountId = accountId
            }, cancellationToken);
    }
}
