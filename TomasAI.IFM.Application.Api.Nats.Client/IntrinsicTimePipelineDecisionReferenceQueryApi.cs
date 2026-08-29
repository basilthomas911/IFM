using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Reference;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Implements generated pipeline decision-reference queries over NATS request/reply.</summary>
public sealed class IntrinsicTimePipelineDecisionReferenceQueryApi(IActorProducer actorProducer)
    : IIntrinsicTimePipelineDecisionReferenceQueryApi
{
    const string ReferenceEntity = "decision-reference";
    readonly IActorProducer _actorProducer = actorProducer ?? throw new ArgumentNullException(nameof(actorProducer));

    public ValueTask<ServiceResult<RegimeDiscoveryDecisionReferenceDto[]>> GetRegimeDiscoveryAsync(
        CancellationToken cancellationToken = default)
    {
        var subject = Subject(GetRegimeDiscoveryDecisionReferenceQuery.Actor,
            GetRegimeDiscoveryDecisionReferenceQuery.Verb);
        return _actorProducer.RequestAsync<RegimeDiscoveryDecisionReferenceDto[],
            GetRegimeDiscoveryDecisionReferenceQuery>(subject, new GetRegimeDiscoveryDecisionReferenceQuery
            {
                Subject = subject,
                EntityId = new ActorEntityId(ReferenceEntity)
            }, cancellationToken);
    }

    public ValueTask<ServiceResult<MarketConditionDecisionReferenceDto[]>> GetMarketConditionAsync(
        CancellationToken cancellationToken = default)
    {
        var subject = Subject(GetMarketConditionDecisionReferenceQuery.Actor,
            GetMarketConditionDecisionReferenceQuery.Verb);
        return _actorProducer.RequestAsync<MarketConditionDecisionReferenceDto[],
            GetMarketConditionDecisionReferenceQuery>(subject, new GetMarketConditionDecisionReferenceQuery
            {
                Subject = subject,
                EntityId = new ActorEntityId(ReferenceEntity)
            }, cancellationToken);
    }

    static ActorSubject Subject(string actor, string verb) =>
        new(ActorType.Query, actor, verb, ReferenceEntity);
}
