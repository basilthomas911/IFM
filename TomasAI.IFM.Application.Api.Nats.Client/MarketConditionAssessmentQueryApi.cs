using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public sealed class MarketConditionAssessmentQueryApi(IActorProducer producer) : IMarketConditionAssessmentQueryApi
{
    public ValueTask<ServiceResult<MarketConditionAssessmentCompletedEvent>> GetAsync(StrategyWorkflowId workflowId,CancellationToken cancellationToken = default)
    {
        var subject = new ActorSubject(ActorType.Query,GetMarketConditionAssessmentQuery.Actor,GetMarketConditionAssessmentQuery.Verb,workflowId.ToString());
        return producer.RequestAsync<MarketConditionAssessmentCompletedEvent,GetMarketConditionAssessmentQuery>(subject,
            new() { Subject=subject,EntityId=new ActorEntityId(workflowId.ToString()),WorkflowId=workflowId },cancellationToken);
    }
    public ValueTask<ServiceResult<MarketConditionAssessmentCompletedEvent[]>> HistoryAsync(string profile,string root,TimeFrameType horizon,DateTime beforeUtc,int pageSize=25,CancellationToken cancellationToken=default)
    {
        var subject = new ActorSubject(ActorType.Query,GetMarketConditionAssessmentHistoryQuery.Actor,GetMarketConditionAssessmentHistoryQuery.Verb,"assessment-history");
        return producer.RequestAsync<MarketConditionAssessmentCompletedEvent[],GetMarketConditionAssessmentHistoryQuery>(subject,
            new() { Subject=subject,EntityId=new ActorEntityId("assessment-history"),MarketProfileId=profile,InstrumentRoot=root,TargetHorizon=horizon,BeforeUtc=beforeUtc,PageSize=pageSize },cancellationToken);
    }
    public ValueTask<ServiceResult<MarketConditionAssessmentCompletedEvent[]>> LatestAsync(string profile,string root,TimeFrameType horizon,CancellationToken cancellationToken=default)
        => HistoryAsync(profile,root,horizon,DateTime.SpecifyKind(DateTime.MaxValue,DateTimeKind.Utc),1,cancellationToken);
}
