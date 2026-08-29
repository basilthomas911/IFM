using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class MarketConditionFunctionExecutionTests
{
    static readonly DateTime Now = new(2026, 8, 27, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Completed_candidate_preserves_frozen_request_metadata()
    {
        var command = Command(Now.AddSeconds(5));

        var terminal = await Execute(command, _ => Task.FromResult<MarketConditionExecutionOutcome>(
            new MarketConditionExecutionCompleted(Result(command))));

        terminal.IsCompleted.Should().BeTrue();
        terminal.Completed.Should().BeEquivalentTo(new
        {
            WorkflowId = command.WorkflowId,
            InputWorkflowRevision = command.InputWorkflowRevision,
            ParameterPayloadSha256 = command.ParameterPayloadSha256,
            ExpiresAtUtc = command.ExpiresAtUtc
        });
        terminal.Completed!.Result.ResultType.Should().Be(nameof(MarketConditionResult));
    }

    [Fact]
    public async Task Expected_failure_returns_typed_failed_terminal()
    {
        var command = Command(Now.AddSeconds(5));

        var terminal = await Execute(command, _ => Task.FromResult<MarketConditionExecutionOutcome>(
            new MarketConditionExecutionFailed(Now, MarketConditionFailureCategory.RequiredInputInvalid,
                MarketConditionReasonCodes.RequiredInput, "source unavailable", Guid.Empty)));

        terminal.IsFailed.Should().BeTrue();
        terminal.Failed!.FailureCategory.Should().Be(MarketConditionFailureCategory.RequiredInputInvalid);
        terminal.Failed.ErrorData.Should().Be(MarketConditionReasonCodes.RequiredInput);
        terminal.Completed.Should().BeNull();
    }

    [Fact]
    public async Task Expired_request_does_not_invoke_worker()
    {
        var invoked = false;
        var command = Command(Now);

        var terminal = await Execute(command, _ =>
        {
            invoked = true;
            return Task.FromResult<MarketConditionExecutionOutcome>(
                new MarketConditionExecutionCompleted(Result(command)));
        });

        invoked.Should().BeFalse();
        terminal.Failed!.FailureCategory.Should().Be(MarketConditionFailureCategory.Timeout);
    }

    [Fact]
    public async Task Timer_winner_is_terminal_and_late_worker_cannot_overwrite_it()
    {
        var command = Command(Now.AddSeconds(5));
        var worker = new TaskCompletionSource<MarketConditionExecutionOutcome>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var execution = ExecuteMarketConditionPipeline.ExecuteAtomicAsync(command,
            new FixedTimeProvider(Now), _ => worker.Task, (_, _) => timer.Task);
        timer.SetResult();
        var terminal = await execution;
        worker.SetResult(new MarketConditionExecutionCompleted(Result(command)));

        terminal.Failed!.FailureCategory.Should().Be(MarketConditionFailureCategory.Timeout);
        terminal.Completed.Should().BeNull();
    }

    [Fact]
    public async Task Completed_state_replays_matching_request_and_rejects_conflicting_fingerprint()
    {
        var command = Command(Now.AddSeconds(5));
        var completed = (await Execute(command, _ => Task.FromResult<MarketConditionExecutionOutcome>(
            new MarketConditionExecutionCompleted(Result(command))))).Completed!;
        var state = new MarketConditionFunctionState();

        state.TryComplete(completed, command).Should().BeTrue();
        state.Matches(command).Should().BeTrue();
        state.Matches(command with { ParameterPayloadSha256 = new string('B', 64) }).Should().BeFalse();
        state.TryComplete(completed, command).Should().BeFalse();
        state.Events.Should().ContainSingle().Which.Should().BeSameAs(completed);
    }

    [Fact]
    public async Task Snapshot_provider_fails_closed_when_adapter_cache_is_empty()
    {
        var provider = new MarketConditionSnapshotProvider();
        provider.Clear();

        var capture = await provider.CaptureAtAsync(Command(Now.AddSeconds(5)), Now);

        capture.Outcome.Should().Be(MarketConditionCaptureOutcome.Failed);
        capture.FailureCategory.Should().Be(MarketConditionFailureCategory.RequiredInputInvalid);
    }

    [Fact]
    public async Task Snapshot_provider_rebinds_one_revision_stable_source_and_seals_hash()
    {
        var provider = new MarketConditionSnapshotProvider();
        provider.Clear();
        var command = Command(Now.AddSeconds(5));
        provider.Upsert(command.FundId, command.InstrumentRoot, command.TargetHorizon, SourceSnapshot());

        var capture = await provider.CaptureAtAsync(command, Now);

        capture.Outcome.Should().Be(MarketConditionCaptureOutcome.Success);
        capture.Snapshot.Should().BeEquivalentTo(new
        {
            WorkflowId = command.WorkflowId,
            EntityId = command.WorkflowEntityId,
            FundId = command.FundId,
            TargetHorizon = command.TargetHorizon,
            EvaluationTimestampUtc = Now
        });
        capture.Snapshot.SnapshotSha256.Should().Be(MarketConditionSnapshotHash.Compute(capture.Snapshot));
        capture.Snapshot.OperationalHealth.Select(x => x.SourceId).Should().BeInAscendingOrder();
        capture.Snapshot.FuturesQuote.QuoteObservation.AgeSeconds.Should().Be(1m);
    }

    static Task<TomasAI.IFM.Shared.EventSourcing.FunctionResult<
        MarketConditionPipelineCompletedEvent,
        MarketConditionPipelineFailedEvent>> Execute(
        ExecuteMarketConditionPipelineCommand command,
        Func<CancellationToken, Task<MarketConditionExecutionOutcome>> worker)
        => ExecuteMarketConditionPipeline.ExecuteAtomicAsync(command, new FixedTimeProvider(Now), worker,
            (_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token));

    static ExecuteMarketConditionPipelineCommand Command(DateTime expiresAtUtc)
    {
        var workflowEntity = IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId(
            "ES-202612", new DateOnly(2026, 8, 27), TimeFrameType.Daily));
        var workflowId = new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000612"));
        var executionId = MarketConditionExecutionEntityId.Create(workflowEntity, workflowId);
        var parameters = MarketConditionParameterSet.CreateDefault(
            Guid.Parse("0198E212-3C00-7000-8000-000000000613"),
            Guid.Parse("0198E212-3C00-7000-8000-000000000614"), 1, TimeFrameType.Daily);
        return new ExecuteMarketConditionPipelineCommand
        {
            CommandId = Guid.Parse("0198E212-3C00-7000-8000-000000000615"),
            Subject = new ActorSubject(ActorType.Function, ExecuteMarketConditionPipelineCommand.Actor,
                ExecuteMarketConditionPipelineCommand.Verb, executionId.Format()),
            EntityId = executionId,
            InputWorkflowRevision = 2,
            WorkflowView = new IntrinsicTimeStrategyWorkflowView
            {
                EntityId = workflowEntity,
                WorkflowId = workflowId,
                WorkflowRevision = 2,
                Status = WorkflowStrategyMachineStatus.Started,
                CurrentStage = StrategyWorkflowStage.MarketCondition,
                FundId = 1,
                MarketConditionParameterSet = parameters,
                MarketConditionParameterPayloadSha256 = MarketConditionParameterPayload.ComputeSha256(parameters),
                ExpiresAtUtc = expiresAtUtc
            },
            TriggerEvent = new FuturesItiSignalGeneratedEvent
            {
                Id = Guid.Parse("0198E212-3C00-7000-8000-000000000616"),
                EntityId = workflowEntity.ItiSignalEntityId
            },
            RequestedAtUtc = Now.AddSeconds(-1),
            ExpiresAtUtc = expiresAtUtc,
            ParameterSet = parameters,
            ParameterPayloadSha256 = MarketConditionParameterPayload.ComputeSha256(parameters),
            TargetHorizon = TimeFrameType.Daily,
            FundId = 1,
            InstrumentRoot = "ES"
        };
    }

    static MarketConditionResult Result(ExecuteMarketConditionPipelineCommand command) => new()
    {
        ResultId = command.CommandId,
        WorkflowId = command.WorkflowId,
        EntityId = command.WorkflowEntityId,
        FundId = command.FundId,
        InstrumentRoot = command.InstrumentRoot,
        TargetHorizon = command.TargetHorizon,
        TriggerEventId = command.TriggerEvent.Id,
        InputWorkflowRevision = command.InputWorkflowRevision,
        MarketConditionParameterSetId = command.ParameterSet.ParameterSetId,
        MarketConditionParameterSetVersion = command.ParameterSet.Version,
        SnapshotId = Guid.Parse("0198E212-3C00-7000-8000-000000000617"),
        SnapshotSha256 = new string('A', 64),
        EvaluatedAtUtc = Now,
        ValidUntilUtc = Now.AddSeconds(30),
        MarketDataAsOfUtc = Now,
        Tradeability = MarketTradeability.Tradeable,
        ConditionType = MarketConditionType.Directional,
        Direction = MarketConditionDirection.Bullish,
        Phase = MarketConditionPhase.Confirmed,
        VolatilityBehavior = MarketConditionVolatilityBehavior.Stable,
        LiquidityQuality = MarketConditionLiquidityQuality.Healthy,
        DataQuality = MarketConditionDataQuality.Healthy,
        UpstreamAlignment = MarketConditionUpstreamAlignment.Aligned,
        PrimaryReasonCode = MarketConditionReasonCodes.Directional,
        Reasons = [MarketConditionReasonCodes.Directional],
        SummaryText = "qualified function result"
    };

    static MarketConditionSnapshot SourceSnapshot()
    {
        var observation = new MarketSourceObservation
        {
            SourceId = "source",
            SourceTimestampUtc = Now.AddSeconds(-1),
            ReceivedAtUtc = Now,
            SequenceId = 10,
            Availability = MarketSourceAvailability.Available,
            Validity = MarketSourceValidity.Valid
        };
        return new MarketConditionSnapshot
        {
            MarketDataAsOfUtc = observation.SourceTimestampUtc,
            FuturesQuote = new MarketConditionFuturesQuote
            {
                BidPrice = 6000m,
                AskPrice = 6000.25m,
                BidSize = 20m,
                AskSize = 20m,
                LastPrice = 6000m,
                QuoteObservation = observation with { SourceId = "futures-quote" },
                TradeObservation = observation with { SourceId = "futures-trade" }
            },
            OptionChainQuality = new MarketConditionOptionChainQuality
            {
                Observation = observation with { SourceId = "option-chain" }
            },
            SessionState = new MarketConditionSessionState
            {
                Observation = observation with { SourceId = "session" }
            },
            EventRiskState = new MarketConditionEventRiskState
            {
                Observation = observation with { SourceId = "event-risk" }
            },
            VolatilityShockState = new MarketConditionVolatilityShockState
            {
                Observation = observation with { SourceId = "volatility" }
            },
            OperationalHealth =
            [
                new MarketConditionOperationalHealthItem
                    { SourceId = "Z", Observation = observation with { SourceId = "Z" } },
                new MarketConditionOperationalHealthItem
                    { SourceId = "A", Observation = observation with { SourceId = "A" } }
            ],
            DataQualityItems = [observation with { SourceId = "data-quality" }]
        };
    }

    sealed class FixedTimeProvider(DateTime value) : TimeProvider
    {
        readonly DateTimeOffset _now = new(value);
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
