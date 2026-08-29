using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class MarketConditionWorkflowTransitionTests
{
    static readonly DateTime Now = new(2026, 8, 27, 14, 0, 10, DateTimeKind.Utc);

    [Fact]
    public void Tradeable_result_advances_exactly_once_to_trade_selection()
    {
        var (state, view) = MarketConditionState();
        var command = Complete(view, Result(view, MarketTradeability.Tradeable));

        command.Execute(Context(Now), state);

        state.CurrentView.Should().BeEquivalentTo(new
        {
            Status = WorkflowStrategyMachineStatus.Started,
            Outcome = StrategyWorkflowOutcome.None,
            CurrentStage = StrategyWorkflowStage.TradeSelection,
            WorkflowRevision = view.WorkflowRevision + 1
        });
        state.CurrentView!.MarketCondition.ContinuationDecision.Should()
            .Be(StrategyWorkflowContinuationDecision.Proceed);
        state.CurrentView.TradeSelection.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Processing);
        state.Events.Should().ContainSingle();
    }

    [Fact]
    public void Not_tradeable_result_closes_workflow_with_explicit_no_trade_outcome()
    {
        var (state, view) = MarketConditionState();
        var result = Result(view, MarketTradeability.NotTradeable);

        Complete(view, result).Execute(Context(Now), state);

        state.CurrentView.Should().BeEquivalentTo(new
        {
            Status = WorkflowStrategyMachineStatus.Completed,
            Outcome = StrategyWorkflowOutcome.NoTrade,
            CurrentStage = StrategyWorkflowStage.MarketCondition,
            StopReasonCode = result.PrimaryReasonCode
        });
        state.CurrentView!.MarketCondition.ContinuationDecision.Should()
            .Be(StrategyWorkflowContinuationDecision.Stop);
        state.CurrentView.TradeSelection.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.NotStarted);
    }

    [Fact]
    public void Expired_completed_result_times_out_and_never_dispatches_next_stage()
    {
        var (state, view) = MarketConditionState();
        var expired = Result(view, MarketTradeability.Tradeable) with
        {
            EvaluatedAtUtc = Now.AddSeconds(-2),
            ValidUntilUtc = Now
        };

        Complete(view, expired).Execute(Context(Now), state);

        state.CurrentView.Should().BeEquivalentTo(new
        {
            Status = WorkflowStrategyMachineStatus.TimedOut,
            Outcome = StrategyWorkflowOutcome.TimedOut,
            CurrentStage = StrategyWorkflowStage.MarketCondition,
            StopReasonCode = MarketConditionReasonCodes.ResultExpired
        });
        state.CurrentView!.TradeSelection.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.NotStarted);
    }

    [Fact]
    public void Identity_conflicting_payload_fails_closed_as_invalid_result()
    {
        var (state, view) = MarketConditionState();
        var conflicting = Result(view, MarketTradeability.Tradeable) with { FundId = view.FundId + 1 };

        Complete(view, conflicting).Execute(Context(Now), state);

        state.CurrentView.Should().BeEquivalentTo(new
        {
            Status = WorkflowStrategyMachineStatus.Failed,
            Outcome = StrategyWorkflowOutcome.InvalidResult,
            StopReasonCode = MarketConditionReasonCodes.ContractInvalid
        });
        state.CurrentView!.TradeSelection.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.NotStarted);
    }

    [Fact]
    public void Typed_timeout_failure_is_authoritative_and_duplicate_is_a_no_op()
    {
        var (state, view) = MarketConditionState();
        var source = Guid.NewGuid();
        var command = new FailMarketConditionCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = Subject(view, FailMarketConditionCommand.Verb),
            EntityId = view.EntityId,
            WorkflowId = view.WorkflowId,
            InputWorkflowRevision = view.WorkflowRevision,
            SourceEventId = source,
            FailureCategory = MarketConditionFailureCategory.Timeout,
            Failure = new StrategyPipelineFailure
            {
                ErrorCode = 24006,
                ErrorMessage = "deadline",
                ErrorType = "Command",
                FailedAtUtc = Now
            },
            CorrelationId = view.CorrelationId,
            CausationId = source,
            FailedAtUtc = Now
        };

        command.Execute(Context(Now), state);
        var accepted = MessagePackSerializer.Serialize(state.CurrentView);
        state.Events.Clear();
        command.Execute(Context(Now.AddMilliseconds(1)), state);

        state.CurrentView.Should().BeEquivalentTo(new
        {
            Status = WorkflowStrategyMachineStatus.TimedOut,
            Outcome = StrategyWorkflowOutcome.TimedOut
        });
        MessagePackSerializer.Serialize(state.CurrentView).Should().Equal(accepted);
        state.Events.Should().BeEmpty();
    }

    static (IntrinsicTimeStrategyWorkflowCommandState State, IntrinsicTimeStrategyWorkflowView View)
        MarketConditionState()
    {
        var started = IntrinsicTimeStrategyWorkflowCommandStateTests.CreateStartedSnapshotForQualification();
        var view = started.State with
        {
            CurrentStage = StrategyWorkflowStage.MarketCondition,
            WorkflowRevision = 2,
            UpdatedAtUtc = Now.AddSeconds(-1),
            RegimeDiscovery = started.State.RegimeDiscovery with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Completed,
                CompletedAtUtc = Now.AddSeconds(-2)
            },
            MarketCondition = started.State.MarketCondition with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Processing,
                StartedAtUtc = Now.AddSeconds(-1),
                InputWorkflowRevision = 2,
                ExpiresAtUtc = started.State.ExpiresAtUtc
            }
        };
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(started with { State = view, WorkflowRevision = view.WorkflowRevision }, addEvent: false)
            .Should().BeTrue();
        return (state, view);
    }

    static CompleteMarketConditionCommand Complete(
        IntrinsicTimeStrategyWorkflowView view,
        MarketConditionResult result)
    {
        var payload = MessagePackSerializer.Serialize(result);
        var envelope = StrategyStageResultEnvelope.Create(result.ResultId, nameof(MarketConditionResult),
            MarketConditionResult.CurrentSchemaVersion, payload, result.MarketDataAsOfUtc, result.EvaluatedAtUtc);
        return new CompleteMarketConditionCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = Subject(view, CompleteMarketConditionCommand.Verb),
            EntityId = view.EntityId,
            WorkflowId = view.WorkflowId,
            InputWorkflowRevision = view.WorkflowRevision,
            SourceEventId = result.ResultId,
            Result = envelope,
            CorrelationId = view.CorrelationId,
            CausationId = result.ResultId,
            CompletedAtUtc = result.EvaluatedAtUtc
        };
    }

    static MarketConditionResult Result(
        IntrinsicTimeStrategyWorkflowView view,
        MarketTradeability tradeability)
        => new()
        {
            ResultId = Guid.NewGuid(),
            WorkflowId = view.WorkflowId,
            EntityId = view.EntityId,
            FundId = view.FundId,
            InstrumentRoot = view.MarketConditionParameterSet.InstrumentRoot,
            TargetHorizon = view.TriggerEvent.EntityId.TimePeriod,
            TriggerEventId = view.TriggerEvent.Id == Guid.Empty
                ? view.TriggerEvent.CommandId
                : view.TriggerEvent.Id,
            InputWorkflowRevision = view.WorkflowRevision,
            StrategyParameterSetId = view.MarketConditionParameterSet.StrategyParameterSetId,
            StrategyParameterSetVersion = view.MarketConditionParameterSet.StrategyParameterSetVersion,
            MarketConditionParameterSetId = view.MarketConditionParameterSet.ParameterSetId,
            MarketConditionParameterSetVersion = view.MarketConditionParameterSet.Version,
            SnapshotId = Guid.NewGuid(),
            SnapshotSha256 = new string('A', 64),
            EvaluatedAtUtc = Now.AddSeconds(-1),
            ValidUntilUtc = Now.AddSeconds(29),
            MarketDataAsOfUtc = Now.AddSeconds(-1),
            Tradeability = tradeability,
            ConditionType = tradeability == MarketTradeability.Tradeable
                ? MarketConditionType.Directional
                : MarketConditionType.NoOpportunity,
            Direction = tradeability == MarketTradeability.Tradeable
                ? MarketConditionDirection.Bullish
                : MarketConditionDirection.Neutral,
            Phase = MarketConditionPhase.Confirmed,
            Strength = tradeability == MarketTradeability.Tradeable ? 80m : 40m,
            Confidence = tradeability == MarketTradeability.Tradeable ? 0.8m : 0.5m,
            VolatilityBehavior = MarketConditionVolatilityBehavior.Stable,
            LiquidityQuality = MarketConditionLiquidityQuality.Healthy,
            DataQuality = MarketConditionDataQuality.Healthy,
            UpstreamAlignment = MarketConditionUpstreamAlignment.Aligned,
            PrimaryReasonCode = tradeability == MarketTradeability.Tradeable
                ? MarketConditionReasonCodes.Directional
                : MarketConditionReasonCodes.Strength,
            Reasons = tradeability == MarketTradeability.Tradeable
                ? [MarketConditionReasonCodes.Directional]
                : [MarketConditionReasonCodes.Strength],
            SummaryText = "qualified test result"
        };

    static ActorSubject Subject(IntrinsicTimeStrategyWorkflowView view, string verb)
        => new(ActorType.Command, ExecuteIntrinsicTimeStrategyWorkflowCommand.Actor, verb, view.EntityId.Format());

    static IIntrinsicTimeStrategyWorkflowCommandContext Context(DateTime now)
    {
        var context = Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();
        context.TimeProvider.Returns(new FixedTimeProvider(new DateTimeOffset(now, TimeSpan.Zero)));
        context.Logger.Returns(Substitute.For<ILogger<IntrinsicTimeStrategyWorkflowCommandActor>>());
        return context;
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
