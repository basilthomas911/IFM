using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

/// <summary>Representative, explicitly non-authoritative examples calculated by the production evaluator.</summary>
public sealed class MarketConditionAssessmentReferenceGenerator
{
    public MarketConditionAssessmentReferenceRow[] Generate() =>
        (from horizon in new[] { TimeFrameType.Daily,TimeFrameType.Weekly,TimeFrameType.Monthly }
         from scenario in new[] { "directional","range","transition","expansion","contraction","dislocated","unclassified","poor","unavailable","restricted" }
         let sample = CreateScenario(horizon,scenario)
         select new MarketConditionAssessmentReferenceRow { CaseCode=$"{horizon}.{scenario}",Result=new MarketConditionAssessmentCalculator().Calculate(sample.Command,sample.Snapshot,sample.Command.CommandId) }).ToArray();

    public static (ExecuteMarketConditionAssessmentCommand Command,MarketConditionAssessmentSnapshot Snapshot) CreateScenario(TimeFrameType horizon,string scenario)
    {
        var at=new DateTime(2026,8,28,14,0,0,DateTimeKind.Utc);
        Guid Id(string name)=>new(SHA256.HashData(Encoding.UTF8.GetBytes($"assessment-reference|{horizon}|{scenario}|{name}")).AsSpan(0,16));
        var signalId=FuturesItiSignalEntityId.Create("ESZ6",DateOnly.FromDateTime(at),horizon);
        var entity=IntrinsicTimeStrategyWorkflowEntityId.Create(signalId);
        var workflow=new StrategyWorkflowId(Id("workflow"));
        var trigger=new FuturesItiSignalGeneratedEvent { Id=Id("trigger"),CommandId=Id("trigger"),EntityId=signalId,CreatedOn=at.AddSeconds(-1),ReceivedOn=at,
            FuturesItiSignal=new FuturesItiSignalV2ReadModel { ContractId="ESZ6",ValueDate=signalId.ValueDate,TimeFrameStartValueDate=signalId.ValueDate,TimePeriod=horizon,SequenceId=1,IntrinsicTime=at,IntrinsicTimeTrend=IntrinsicTimeTrendType.UpTrend } };
        var rp=RegimeDiscoveryParameterSet.CreateDefault(Id("regime-profile"),Id("strategy"),horizon);
        var p=MarketConditionAssessmentParameterSet.CreateDefault("ES.Reference",horizon,Id("profile"),rp.ParameterSetId,rp.Version);
        var decision=new RegimeDiscoveryDecision
        {
            IsComplete=true,Direction=scenario is "range" or "unclassified"?RegimeDirection.Neutral:RegimeDirection.Up,
            Confidence=.9m,StructureClassification=scenario=="range"?MarketStructureClassification.Ranging:scenario=="transition"?MarketStructureClassification.Transitioning:MarketStructureClassification.Trending,
            VolatilityChange=scenario=="expansion"?VolatilityRegimeChange.Expanding:scenario=="contraction"?VolatilityRegimeChange.Contracting:VolatilityRegimeChange.Stable,
            Restrictions=scenario=="restricted"?[RegimeRestriction.NoNewTrade]:[]
        };
        var regime=new RegimeDiscoveryResult { ResultId=Id("regime"),WorkflowId=workflow,EntityId=entity,TriggerEventId=trigger.Id,TargetHorizon=horizon,
            RegimeDiscoveryParameterSetId=rp.ParameterSetId,RegimeDiscoveryParameterSetVersion=rp.Version,ProducedAtUtc=at.AddSeconds(-1),MarketDataAsOfUtc=at.AddSeconds(-1),Decision=decision };
        var envelope=StrategyStageResultEnvelope.Create(regime.ResultId,nameof(RegimeDiscoveryResult),RegimeDiscoveryResult.CurrentSchemaVersion,MessagePackSerializer.Serialize(regime),regime.MarketDataAsOfUtc,regime.ProducedAtUtc);
        var binding=new MarketConditionAssessmentBinding { Parameters=p,PayloadSha256=MarketConditionAssessmentHash.Parameters(p) };
        var view=new IntrinsicTimeStrategyWorkflowView
        {
            WorkflowId=workflow,EntityId=entity,TriggerEventId=trigger.Id,TriggerEvent=trigger,WorkflowRevision=2,Status=WorkflowStrategyMachineStatus.Started,
            CurrentStage=StrategyWorkflowStage.MarketCondition,UpdatedAtUtc=at,StartedAtUtc=at.AddSeconds(-2),ExpiresAtUtc=at.AddMinutes(1),
            RegimeDiscoveryParameterSet=rp,RegimeDiscoveryParameterPayloadSha256=RegimeDiscoveryParameterPayload.ComputeSha256(rp),AssessmentBinding=binding,
            RegimeDiscovery=new() { ProcessingStatus=StrategyActorProcessingStatus.Completed,CompletedAtUtc=regime.ProducedAtUtc,Result=envelope }
        };
        var id=new MarketConditionAssessmentExecutionId(entity,workflow);
        var command=new ExecuteMarketConditionAssessmentCommand
        {
            CommandId=Id("command"),EntityId=id,Subject=new(ActorType.Function,ExecuteMarketConditionAssessmentCommand.Actor,ExecuteMarketConditionAssessmentCommand.Verb,id.Format()),
            WorkflowView=view,TriggerEvent=trigger,InputWorkflowRevision=2,RequestedAtUtc=at,ExpiresAtUtc=at.AddSeconds(5),ParameterSet=p,ParameterPayloadSha256=binding.PayloadSha256,
            MarketProfileId=p.MarketProfileId,InstrumentRoot=p.InstrumentRoot,TargetHorizon=horizon,RegimeResultEnvelope=envelope,RegimePayloadSha256=envelope.PayloadSha256
        };
        var snapshot=new MarketConditionAssessmentSnapshot
        {
            SnapshotId=Id("snapshot"),MarketProfileId=p.MarketProfileId,InstrumentRoot="ES",TargetHorizon=horizon,ReferenceInstrumentId="ESZ6",EvaluatedAtUtc=at,
            Quote=scenario=="poor"?new(5000,5002,1,1):new(5000,5000.25m,10,10),SessionState=MarketSessionStatus.Open,EventContext=AssessmentEventContext.Clear,
            Observations=p.Sources.Select(x=>new AssessmentObservation
            {
                SourceId=x.SourceId,ObservedAtUtc=at.AddMilliseconds(-500),ReceivedAtUtc=at,Sequence=1,
                Availability=scenario=="unavailable"&&x.SourceId=="FeedHealth"?MarketSourceAvailability.Unavailable:MarketSourceAvailability.Available,
                Validity=MarketSourceValidity.Valid,Value=scenario=="dislocated"&&x.SourceId=="NormalizedMovement"?2m:0m,Unit="ratio"
            }).ToArray(),CalendarEvidence=new() { CheckedAtUtc=at,CoverageConfirmed=true,ValidUntilUtc=at.AddHours(1),Reason="Representative reference fixture; not downloaded market data" }
        }.Seal();
        return(command,snapshot);
    }
}
