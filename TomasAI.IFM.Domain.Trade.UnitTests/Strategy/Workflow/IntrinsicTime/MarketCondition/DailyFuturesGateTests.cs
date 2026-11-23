using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Tests.Support;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class DailyFuturesGateTests
{
    [Theory]
    [InlineData("long", "LongFuture")]
    [InlineData("short", "ShortFuture")]
    public async Task Eligible_gate_reaches_a_validated_selection_with_complete_policy_evidence(string scenario,string variant)
    {
        var (gate,snapshot) = DailyFuturesGateTestScenario.Create(scenario);
        var assessment = new MarketConditionAssessmentCalculator().Calculate(gate,snapshot,gate.CommandId);
        var command = await TradeSelection.TradeSelectionFixture.Command(variant,atUtc:gate.RequestedAtUtc,contractId:gate.TriggerEvent.EntityId.ContractId,compositionReady:true,
            actualAssessmentCommand:gate,actualAssessmentEnvelope:StrategyStageResultEnvelope.CreateAssessment(assessment),preserveAssessmentTime:true);
        var result = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model.TradeSelectionEvaluator.Evaluate(command);
        Assert.Equal(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.SelectionOutcome.Selected,result.Outcome);
        Assert.Equal(21,result.GlobalEvidence.Length);
        Assert.Single(result.CandidateDecisions); Assert.Equal(9,result.CandidateDecisions[0].RuleEvidence.Length);
        var accepted = Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts.ReadResult(StrategyStageResultEnvelope.CreateSelection(result));
        Assert.Equal(result.Outcome,accepted.Outcome);
        var composition = await OrderComposer.CompositionFixture.Command(variant,atUtc:gate.RequestedAtUtc,actualSelectionCommand:command,actualSelectionResult:result);
        var composed = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer(
            new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.Black76ComposerPricer()).Calculate(composition);
        Assert.NotNull(composed.Candidate);
        var riskCommand = await RiskManager.RiskFixture.Command(variant,atUtc:gate.RequestedAtUtc,actualCompositionCommand:composition,actualCompositionResult:composed);
        var risk = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model.RiskEvaluator().Calculate(riskCommand);
        Assert.NotNull(risk);
    }

    public static IEnumerable<object[]> Cases() => DailyFuturesGateTestScenario.Cases();
    [Theory, MemberData(nameof(Cases))]
    public void Eligibility_and_named_rejections_complete_once_without_changing_upstream(string scenario, string? reason)
    {
        var (command, snapshot) = DailyFuturesGateTestScenario.Create(scenario);
        var before = MessagePackSerializer.Serialize(command.TriggerEvent);
        var result = new MarketConditionAssessmentCalculator().Calculate(command, snapshot, command.CommandId);
        new MarketConditionAssessmentCalculator().CalculatePrepared(command, snapshot, command.CommandId)
            .Should().BeEquivalentTo(result);
        Assert.Equal(reason is null ? AssessmentAvailability.Available : AssessmentAvailability.Unavailable, result.Assessment.Availability);
        if (reason is not null) Assert.Contains(DailyFuturesGatePolicy.Prefix + reason, result.Assessment.LimitationReasons);
        else Assert.Empty(result.Assessment.LimitationReasons);
        Assert.Equal(AssessmentVolatility.Unknown, result.Assessment.VolatilityBehavior);
        var envelope = StrategyStageResultEnvelope.CreateAssessment(result);
        Assert.True(envelope.HasValidPayloadSha256());
        MarketConditionAssessmentContracts.ValidateAcceptance(result, command.WorkflowView, command.InputWorkflowRevision);
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(new WorkflowStrategyStateUpdatedEvent { State = command.WorkflowView, WorkflowId = command.WorkflowId,
            WorkflowRevision = command.InputWorkflowRevision, EntityId = command.WorkflowEntityId }, addEvent:false).Should().BeTrue();
        var complete = new CompleteMarketConditionCommand { CommandId = Guid.NewGuid(), EntityId = command.WorkflowEntityId,
            WorkflowId = command.WorkflowId, InputWorkflowRevision = command.InputWorkflowRevision, SourceEventId = result.ResultId,
            CompletedAtUtc = snapshot.EvaluatedAtUtc, Result = envelope,
            Subject = new(ActorType.Command, CompleteMarketConditionCommand.Actor, CompleteMarketConditionCommand.Verb, command.WorkflowEntityId.Format()) };
        var context = Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();
        context.TimeProvider.Returns(new Clock(snapshot.EvaluatedAtUtc));
        context.Logger.Returns(Substitute.For<ILogger<IntrinsicTimeStrategyWorkflowCommandActor>>());
        complete.Execute(context,state);
        if (reason is null) Assert.Equal(StrategyWorkflowStage.TradeSelection, state.CurrentView!.CurrentStage);
        else
        {
            Assert.Equal(StrategyWorkflowOutcome.NoTrade, state.CurrentView!.Outcome);
            Assert.Contains(DailyFuturesGatePolicy.Prefix + reason, state.CurrentView.MarketCondition.ContinuationReasonCodes);
        }
        var accepted = MessagePackSerializer.Serialize(state.CurrentView);
        state.Events.Clear(); complete.Execute(context,state);
        Assert.Equal(accepted,MessagePackSerializer.Serialize(state.CurrentView)); Assert.Empty(state.Events);
        Assert.Equal(before,MessagePackSerializer.Serialize(command.TriggerEvent));
    }

    [Theory]
    [InlineData(2026,11,19,14,29,false)]
    [InlineData(2026,11,19,14,30,true)]
    [InlineData(2026,11,19,20,54,true)]
    [InlineData(2026,11,19,20,55,false)]
    [InlineData(2026,7,1,13,30,true)]
    [InlineData(2026,7,1,19,55,false)]
    [InlineData(2026,11,21,15,0,false)]
    public void Rth_uses_eastern_dst_and_not_the_overnight_session(int y,int m,int d,int h,int minute,bool expected)
        => Assert.Equal(expected,DailyFuturesGateCalculator.IsEntryWindow(new CmeFuturesMarketSessionCalendar(),new(y,m,d,h,minute,0,DateTimeKind.Utc)));

    [Fact]
    public void Early_close_and_holiday_windows_are_respected()
    {
        var date = new DateOnly(2026,11,27);
        var calendar = new CmeFuturesMarketSessionCalendar(earlyCloses:new Dictionary<DateOnly,TimeOnly> { [date] = new(13,0) });
        Assert.True(DailyFuturesGateCalculator.IsEntryWindow(calendar,new(2026,11,27,17,54,0,DateTimeKind.Utc)));
        Assert.False(DailyFuturesGateCalculator.IsEntryWindow(calendar,new(2026,11,27,17,55,0,DateTimeKind.Utc)));
        Assert.False(DailyFuturesGateCalculator.IsEntryWindow(new CmeFuturesMarketSessionCalendar([date]),new(2026,11,27,15,0,0,DateTimeKind.Utc)));
    }

    [Fact]
    public void Old_commands_keep_their_fingerprint_and_new_commands_pin_the_policy_on_the_wire()
    {
        var legacy = AssessmentFixture.Command();
        Assert.DoesNotContain("CalculationPolicy",System.Text.Json.JsonSerializer.Serialize(legacy));
        Assert.Equal(legacy.Fingerprint(),MessagePackSerializer.Deserialize<ExecuteMarketConditionAssessmentCommand>(MessagePackSerializer.Serialize(legacy)).Fingerprint());
        var (command,_) = DailyFuturesGateTestScenario.Create();
        var copy = MessagePackSerializer.Deserialize<ExecuteMarketConditionAssessmentCommand>(MessagePackSerializer.Serialize(command));
        Assert.Equal(DailyFuturesGatePolicy.Version,copy.CalculationPolicy); Assert.Equal(command.Fingerprint(),copy.Fingerprint());
    }

    [Fact]
    public void Accepted_evidence_expires_at_entry_cutoff_and_other_families_cannot_select_this_policy()
    {
        var (command,snapshot) = DailyFuturesGateTestScenario.Create(timestamp:new DateTime(2026,11,19,20,54,59,DateTimeKind.Utc));
        var result = new MarketConditionAssessmentCalculator().Calculate(command,snapshot,command.CommandId);
        Assert.True(result.Assessment.ValidUntilUtc > new DateTime(2026,11,19,20,55,0,DateTimeKind.Utc));
        foreach(var family in new[] { "IronCondor", "VerticalSpread" })
        {
            var view = command.WorkflowView with { EntityId = command.WorkflowEntityId with { Assignment = command.WorkflowEntityId.Assignment! with { Family = family } } };
            Assert.False(DailyFuturesGatePolicy.Matches(view));
            Assert.False(DailyFuturesGatePolicy.Applies(view.EntityId,result.Assessment));
        }
    }
    [Theory]
    [InlineData("shallow", "LIQUIDITY_POOR")]
    [InlineData("no-baseline", "ATR_NOT_READY")]
    [InlineData("invalid-baseline", "ATR_NOT_READY")]
    [InlineData("stale-baseline", "ATR_STALE")]
    public void Already_issued_v1_commands_keep_their_original_vetoes(string scenario, string reason)
    {
        var (command,snapshot) = DailyFuturesGateTestScenario.Create(scenario);
        command = command with { CalculationPolicy = DailyFuturesGatePolicy.LegacyVersion };
        var result = new MarketConditionAssessmentCalculator().Calculate(command,snapshot,command.CommandId);
        Assert.Equal(AssessmentAvailability.Unavailable,result.Assessment.Availability);
        Assert.StartsWith("Daily futures: No Trade; ",result.SummaryText);
        Assert.Contains(DailyFuturesGatePolicy.Prefix + reason,result.Assessment.LimitationReasons);
        Assert.True(DailyFuturesGatePolicy.Applies(command.WorkflowEntityId,result.Assessment));
        Assert.Contains(result.Assessment.EvidenceItems,e => e.SourceId == DailyFuturesGatePolicy.LegacyVersion && e.Feature == "Policy");
    }
    sealed class Clock(DateTime at) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(at); }
}
