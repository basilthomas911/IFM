using NSubstitute;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
namespace TomasAI.IFM.Tests.Support;
internal static class AssessmentSeparationScenario
{
    internal static IEnumerable<object[]> Cases() => new (string,TimeFrameType,string,string?)[] {
        ("ShortFuture",TimeFrameType.Daily,"non-dc",null),
        ("ShortFuture",TimeFrameType.Daily,"opposed",null),
        ("ShortFuture",TimeFrameType.Daily,"premarket",null),
        ("ShortFuture",TimeFrameType.Daily,"wide","TS.ASSESSMENT.LIQUIDITY"),
        ("ShortFuture",TimeFrameType.Daily,"blackout","TS.ASSESSMENT.EVENT"),
        ("ShortFuture",TimeFrameType.Daily,"closed","TS.ASSESSMENT.SESSION"),
        ("ShortFuture",TimeFrameType.Daily,"inherited","TS.REGIME.RESTRICTION"),
        ("ShortFuture",TimeFrameType.Daily,"feed-unhealthy","TS.ASSESSMENT.DATA_QUALITY"),
        ("BullPutCredit",TimeFrameType.Weekly,"non-dc",null),
        ("BullPutCredit",TimeFrameType.Weekly,"blackout","TS.ASSESSMENT.EVENT"),
        ("ShortBalancedIronCondor",TimeFrameType.Monthly,"range",null),
        ("ShortBalancedIronCondor",TimeFrameType.Monthly,"premarket",null),
        ("ShortBalancedIronCondor",TimeFrameType.Monthly,"blackout","TS.ASSESSMENT.EVENT"),
        ("ShortBalancedIronCondor",TimeFrameType.Monthly,"closed","TS.ASSESSMENT.SESSION")
    }.Select(x=>new object[]{x.Item1,x.Item2,x.Item3,x.Item4!});

    internal static async Task<(ExecuteMarketConditionAssessmentCommand Request,MarketConditionAssessmentSnapshot Snapshot,
        MarketConditionAssessmentResult Assessment,ExecuteTradeSelectionPipelineCommand Selection,TradeSelectionResult Result)> Create(string variant,TimeFrameType horizon,string scenario)
    {
        var at = new DateTime(2026,11,19,scenario == "premarket" ? 13 : 15,0,0,DateTimeKind.Utc);
        var template = await TradeSelectionFixture.Command(variant,horizon,atUtc:at);
        ExecuteMarketConditionAssessmentCommand request;
        MarketConditionAssessmentSnapshot snapshot;
        if (horizon == TimeFrameType.Daily)
            (request,snapshot) = DailyFuturesGateTestScenario.Create(scenario == "non-dc" ? "not-dc" : scenario == "premarket" ? "long" : scenario,timestamp:at);
        else
        {
            request = AssessmentFixture.Command(horizon,at,"ES20261218");
            snapshot = TradeSelectionFixture.Snapshot(request) with { ReferenceInstrumentId = "ES20261218",
                Observations = request.ParameterSet.Sources.Select(binding => new AssessmentObservation { SourceId=binding.SourceId,
                    ObservedAtUtc=at,ReceivedAtUtc=at,Sequence=1,Availability=MarketSourceAvailability.Available,Validity=MarketSourceValidity.Valid,
                    Value=binding.SourceId == "FeedHealth" ? 1 : binding.SourceId == "LastTrade" ? 5000 : 0 }).ToArray(),
                SessionState = scenario == "closed" ? MarketSessionStatus.Closed : MarketSessionStatus.Open,
                EventContext = scenario == "blackout" ? AssessmentEventContext.Elevated : AssessmentEventContext.Clear };
        }
        var decision = template.RegimeResultEnvelope.ReadRegimeResult().Decision;
        if (scenario == "inherited") decision = decision with { Restrictions = [RegimeRestriction.NoNewTrade] };
        var regime = request.RegimeResultEnvelope.ReadRegimeResult() with { Reasons=[new(){Code=EmaStackRegimePolicy.ReasonCode}],Decision=decision };
        var regimeEnvelope = StrategyStageResultEnvelope.CreateRegime(regime);
        var signal = request.TriggerEvent.FuturesItiSignal! with { IntrinsicTimeMode=IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeTrend = scenario == "opposed" ? IntrinsicTimeTrendType.UpTrend : IntrinsicTimeTrendType.DownTrend };
        var trigger = request.TriggerEvent with { FuturesItiSignal=signal };
        var view = request.WorkflowView with { TriggerEvent=trigger,RegimeDiscovery=request.WorkflowView.RegimeDiscovery with {Result=regimeEnvelope} };
        request = request with { WorkflowView=view,TriggerEvent=trigger,RegimeResultEnvelope=regimeEnvelope,RegimePayloadSha256=regimeEnvelope.PayloadSha256,
            CalculationPolicy=horizon == TimeFrameType.Daily ? DailyFuturesGatePolicy.Version : MarketConditionObservationPolicy.Version };
        snapshot = snapshot.Seal();
        var assessment = new MarketConditionAssessmentCalculator().Calculate(request,snapshot,request.CommandId);
        var selection = await TradeSelectionFixture.Command(variant,horizon,atUtc:at,actualAssessmentCommand:request,
            actualAssessmentEnvelope:StrategyStageResultEnvelope.CreateAssessment(assessment),preserveAssessmentTime:true);
        // Exercise the real workflow transitions: complete RD, then MC, and use
        // the dispatch produced by the workflow manager rather than fabricating a selection request.
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        var bound = request.WorkflowView with { SelectionBinding=selection.SelectionBinding,FundId=selection.WorkflowView.FundId,
            WorkflowRevision=1,CurrentStage=StrategyWorkflowStage.RegimeDiscovery,
            RegimeDiscovery=request.WorkflowView.RegimeDiscovery with { ProcessingStatus=StrategyActorProcessingStatus.Processing,SourceEventId=Guid.Empty } };
        state.Apply(new WorkflowStrategyStateUpdatedEvent { State=bound,WorkflowId=bound.WorkflowId,WorkflowRevision=1,EntityId=bound.EntityId },addEvent:false);
        var context=Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();
        context.TimeProvider.Returns(new Clock(at));context.Logger.Returns(Substitute.For<ILogger<IntrinsicTimeStrategyWorkflowCommandActor>>());
        var regimeCompletion=new CompleteRegimeDiscoveryCommand { CommandId=Guid.NewGuid(),EntityId=bound.EntityId,WorkflowId=bound.WorkflowId,
            InputWorkflowRevision=1,SourceEventId=regime.ResultId,Result=regimeEnvelope,CompletedAtUtc=at,
            Subject=new(ActorType.Command,CompleteRegimeDiscoveryCommand.Actor,CompleteRegimeDiscoveryCommand.Verb,bound.EntityId.Format()) };
        if(!regimeCompletion.Execute(context,state).Success || state.CurrentView?.CurrentStage != StrategyWorkflowStage.MarketCondition)
            throw new InvalidOperationException("Valid regime did not progress to Market Condition.");
        var assessmentCompletion=new CompleteMarketConditionCommand { CommandId=Guid.NewGuid(),EntityId=bound.EntityId,WorkflowId=bound.WorkflowId,
            InputWorkflowRevision=2,SourceEventId=assessment.ResultId,Result=StrategyStageResultEnvelope.CreateAssessment(assessment),CompletedAtUtc=at,
            Subject=new(ActorType.Command,CompleteMarketConditionCommand.Actor,CompleteMarketConditionCommand.Verb,bound.EntityId.Format()) };
        if(!assessmentCompletion.Execute(context,state).Success || state.CurrentView?.MarketCondition.ContinuationDecision != StrategyWorkflowContinuationDecision.Proceed)
            throw new InvalidOperationException("Available assessment did not progress to Trade Selection.");
        selection=state.CurrentView.SelectionDispatch ?? throw new InvalidOperationException("Workflow failed to initialize selection: " + state.CurrentView.StopReasonCode + " / " + state.CurrentView.TradeSelection.Failure?.ErrorMessage + " / " + state.CurrentView.TradeSelection.Failure?.ErrorData);
        var result = TradeSelectionEvaluator.Evaluate(selection);
        TradeSelectionContracts.ReadResult(StrategyStageResultEnvelope.CreateSelection(result));
        return (request,snapshot,assessment,selection,result);
    }
    sealed class Clock(DateTime at):TimeProvider { public override DateTimeOffset GetUtcNow()=>new(at); }
}
