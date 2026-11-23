using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Tests.Support;
internal static class OptionVxVetoScenario
{
    internal static IEnumerable<object[]> Cases()=>from frame in new[]{TimeFrameType.Weekly,TimeFrameType.Monthly}
        from scenario in new[]{"backwardation","flat","contango","missing","stale","zero","duplicate","blackout"}
        select new object[]{frame,scenario};
    internal static async Task<(ExecuteMarketConditionAssessmentCommand Request,MarketConditionAssessmentSnapshot Snapshot,MarketConditionAssessmentResult Result)> Create(TimeFrameType frame,string scenario)
    {
        var basis=await AssessmentSeparationScenario.Create(frame==TimeFrameType.Monthly?"ShortBalancedIronCondor":"BullPutCredit",frame,scenario=="blackout"?"blackout":"range");
        var c=basis.Request;var at=c.RequestedAtUtc;
        var entity=c.WorkflowEntityId with {Assignment=new(1,1,"Options",frame==TimeFrameType.Monthly?"IronCondor":"Vertical","ES",frame,default,1,at.AddDays(-1),null)};
        var regime=c.RegimeResultEnvelope.ReadRegimeResult() with {EntityId=entity};
        var envelope=StrategyStageResultEnvelope.CreateRegime(regime);
        var view=c.WorkflowView with {EntityId=entity,RegimeDiscovery=c.WorkflowView.RegimeDiscovery with{Result=envelope}};
        var id=new MarketConditionAssessmentExecutionId(entity,c.WorkflowId);
        c=c with{EntityId=id,WorkflowView=view,CalculationPolicy=MarketConditionObservationPolicy.OptionVxVersion,
            RegimeResultEnvelope=envelope,RegimePayloadSha256=envelope.PayloadSha256,
            Subject=new(c.Subject.ActorType,ExecuteMarketConditionAssessmentCommand.Actor,ExecuteMarketConditionAssessmentCommand.Verb,id.Format())};
        var front=new AssessmentObservation{SourceId="VxFront",ObservedAtUtc=at,ReceivedAtUtc=at,Sequence=10,
            Availability=MarketSourceAvailability.Available,Validity=MarketSourceValidity.Valid,Value=scenario=="backwardation"?22:scenario=="flat"?21:20,Reason="VX20261216",Unit="VX points"};
        var second=front with{SourceId="VxSecond",Value=21,Reason=scenario=="duplicate"?front.Reason:"VX20270120"};
        if(scenario=="missing")second=second with{Availability=MarketSourceAvailability.Unavailable,Value=null};
        if(scenario=="stale")second=second with{ObservedAtUtc=at.AddMinutes(-10)};
        if(scenario=="zero")second=second with{Value=0};
        var snapshot=(basis.Snapshot with{Observations=[..basis.Snapshot.Observations,front,second]}).Seal();
        return(c,snapshot,new MarketConditionAssessmentCalculator().Calculate(c,snapshot,c.CommandId));
    }
}

