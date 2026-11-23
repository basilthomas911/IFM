using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Tests.Support;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;
public sealed class AssessmentSeparationTests
{
    public static IEnumerable<object[]> Cases()=>AssessmentSeparationScenario.Cases();
    [Theory,MemberData(nameof(Cases))]
    public async Task Known_conditions_proceed_to_selection_which_owns_eligibility(string variant,TimeFrameType frame,string scenario,string? reason)
    {
        var x=await AssessmentSeparationScenario.Create(variant,frame,scenario);
        Assert.Equal(AssessmentAvailability.Available,x.Assessment.Assessment.Availability);
        Assert.True(x.Request.RegimeResultEnvelope.ReadRegimeResult().Decision.IsComplete);
        Assert.Equal(AssessmentTriggerAlignment.NotApplicable,x.Assessment.Assessment.TriggerAlignment);
        Assert.True(x.Result.Outcome == (reason is null?SelectionOutcome.Selected:SelectionOutcome.NoTrade),
            x.Result.PrimaryReasonCode + ": " + string.Join(",",x.Result.GlobalEvidence.Where(e=>e.Status==SelectionRuleStatus.Rejected).Select(e=>e.ReasonCode)) + "; " + string.Join(",",x.Result.CandidateDecisions.SelectMany(d=>d.ReasonCodes)));
        if(reason is not null)Assert.Contains(x.Result.GlobalEvidence,e=>e.ReasonCode==reason && e.Status==SelectionRuleStatus.Rejected);
        var view=x.Selection.WorkflowView with {Outcome=reason is null?StrategyWorkflowOutcome.None:StrategyWorkflowOutcome.NoTrade,
            MarketCondition=x.Selection.WorkflowView.MarketCondition with {ContinuationDecision=StrategyWorkflowContinuationDecision.Proceed,ReasonSentence="Market Condition returned no eligible trade."}};
        var row=IntrinsicTimeStrategyUpdatesGridRow.FromView(view);
        Assert.Contains("assessment available",row.Stages[1].ReasonSentence);
        Assert.DoesNotContain("no eligible trade",row.Stages[1].ReasonSentence);
        Assert.Contains("Trade Selection decides",row.Stages[1].ReasonSentence);
        Assert.True(view.MarketCondition.Result!.HasValidPayloadSha256());
        Assert.Equal("Market Condition returned no eligible trade.",view.MarketCondition.ReasonSentence);
        var restored=MessagePackSerializer.Deserialize<Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteTradeSelectionPipelineCommand>(MessagePackSerializer.Serialize(x.Selection));
        Assert.Equal(x.Selection.Fingerprint(),restored.Fingerprint());
    }
    [Fact]
    public void Missing_inputs_show_every_reason_without_truncation_or_mutating_stored_state()
    {
        var (command,snapshot)=DailyFuturesGateTestScenario.Create("cold-atr");
        snapshot=(snapshot with {Observations=snapshot.Observations.Select(o=>o.SourceId is "ReferenceQuote" or "FeedHealth"
            ? o with{Availability=MarketSourceAvailability.Unavailable}:o).ToArray()}).Seal();
        var result=new MarketConditionAssessmentCalculator().Calculate(command,snapshot,command.CommandId);
        var view=command.WorkflowView with {Outcome=StrategyWorkflowOutcome.NoTrade,
            MarketCondition=command.WorkflowView.MarketCondition with {ProcessingStatus=StrategyActorProcessingStatus.Completed,
                ContinuationDecision=StrategyWorkflowContinuationDecision.Stop,Result=StrategyStageResultEnvelope.CreateAssessment(result),
                ContinuationReasonCodes=result.Assessment.LimitationReasons,ReasonSentence="Market Condition returned no eligible trade."}};
        var tooltip=IntrinsicTimeStrategyUpdatesGridRow.FromView(view).Stages[1].ReasonSentence!;
        foreach(var reason in result.Assessment.LimitationReasons)Assert.Contains(reason,tooltip);
        Assert.Contains("ATR(14)",tooltip);Assert.Contains("bid/ask",tooltip);Assert.Contains("feed-health",tooltip);
        Assert.Contains("no strategy was evaluated",tooltip);Assert.True(tooltip.Length>200);
        Assert.DoesNotContain("no eligible trade",tooltip);
        Assert.True(view.MarketCondition.Result!.HasValidPayloadSha256());
    }
    [Fact]
    public void Historical_available_assessment_explains_its_legacy_continuation_stop()
    {
        var (command,snapshot)=DailyFuturesGateTestScenario.Create();
        command=command with {CalculationPolicy=DailyFuturesGatePolicy.PreviousVersion};
        var result=new MarketConditionAssessmentCalculator().Calculate(command,snapshot,command.CommandId);
        var view=command.WorkflowView with {Outcome=StrategyWorkflowOutcome.NoTrade,
            MarketCondition=command.WorkflowView.MarketCondition with {ProcessingStatus=StrategyActorProcessingStatus.Completed,
                ContinuationDecision=StrategyWorkflowContinuationDecision.Stop,Result=StrategyStageResultEnvelope.CreateAssessment(result),
                ContinuationReasonCodes=["MC.ASSESSMENT.INHERITED_NO_NEW_TRADE"],ReasonSentence="Market Condition returned no eligible trade."}};
        var tooltip=IntrinsicTimeStrategyUpdatesGridRow.FromView(view).Stages[1].ReasonSentence!;
        Assert.Contains("legacy",tooltip);Assert.Contains("NoNewTrade restriction",tooltip);Assert.Contains("Trade Selection did not start",tooltip);
        Assert.DoesNotContain("Trade Selection decides strategy eligibility",tooltip);
    }

}
