using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.BDDTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Executable RD-19D/E business scenarios for fail-closed workflow progression.</summary>
public sealed class IntrinsicTimeStrategyWorkflowAtomicScenarios
{
    static readonly DateTime StartedAt = new(2026, 8, 27, 16, 0, 0, DateTimeKind.Utc);
    static readonly TimeSpan MaximumDuration = TimeSpan.FromMinutes(2);

    /// <summary>Given Free, Start commits a Started snapshot before any Regime execution can exist.</summary>
    [Theory]
    [InlineData(TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public void Start_produces_only_the_committed_started_snapshot(TimeFrameType period)
    {
        var scenario = new Scenario(period);

        scenario.Start(StartedAt);

        scenario.State.Events.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowStrategyStateUpdatedEvent>();
        scenario.State.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.Started);
        scenario.State.CurrentView.ExpiresAtUtc.Should().Be(StartedAt.Add(MaximumDuration));
        scenario.State.Events.Should().NotContain(value => value.EventName.Contains("Execute", StringComparison.Ordinal));
    }

    /// <summary>Given a valid Regime completion, the committed view alone selects the next stage.</summary>
    [Fact]
    public void Valid_completion_advances_to_market_condition()
    {
        var scenario = new Scenario(TimeFrameType.Daily);
        scenario.StartAndReload(StartedAt);

        scenario.Complete(StartedAt.AddSeconds(10));

        scenario.State.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.Started);
        scenario.State.CurrentView.CurrentStage.Should().Be(StrategyWorkflowStage.MarketCondition);
        scenario.State.CurrentView.RegimeDiscovery.Result.Should().NotBeNull();
    }

    /// <summary>Given Regime fails, Workflow becomes Failed and cannot select another pipeline.</summary>
    [Fact]
    public void Regime_failure_stops_without_next_pipeline()
    {
        var scenario = new Scenario(TimeFrameType.Daily);
        scenario.StartAndReload(StartedAt);

        scenario.Fail(StartedAt.AddSeconds(10), "RequiredSignalMissing");

        scenario.State.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.Failed);
        scenario.State.CurrentView.CurrentStage.Should().Be(StrategyWorkflowStage.RegimeDiscovery);
        scenario.State.CurrentView.RegimeDiscovery.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Failed);
    }

    /// <summary>A timeout-classified private failure definitively times out the workflow.</summary>
    [Fact]
    public void Regime_timeout_stops_without_next_pipeline()
    {
        var scenario = new Scenario(TimeFrameType.Daily);
        scenario.StartAndReload(StartedAt);

        scenario.Fail(StartedAt.AddSeconds(10), "RegimeDiscoveryTimedOut");

        scenario.State.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.TimedOut);
        scenario.State.CurrentView.CurrentStage.Should().Be(StrategyWorkflowStage.RegimeDiscovery);
    }

    /// <summary>Lost post-commit notification leaves the authoritative workflow Started and unable to advance.</summary>
    [Fact]
    public void Lost_started_notification_leaves_workflow_started_without_progress()
    {
        var scenario = new Scenario(TimeFrameType.Daily);
        scenario.StartAndReload(StartedAt);

        scenario.State.Events.Should().BeEmpty("restart loaded state but emitted no notification or dispatch");
        scenario.State.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.Started);
        scenario.State.CurrentView.CurrentStage.Should().Be(StrategyWorkflowStage.RegimeDiscovery);
    }

    /// <summary>An expired workflow is closed and replaced atomically; its late completion cannot affect the new one.</summary>
    [Fact]
    public void Expired_replacement_ignores_late_completion_from_old_workflow()
    {
        var scenario = new Scenario(TimeFrameType.Daily);
        scenario.StartAndReload(StartedAt);
        var oldWorkflowId = scenario.WorkflowId;

        scenario.Replace(StartedAt.AddMinutes(3));

        scenario.State.Events.Cast<WorkflowStrategyStateUpdatedEvent>().Select(value => value.State.Status)
            .Should().Equal(WorkflowStrategyMachineStatus.TimedOut, WorkflowStrategyMachineStatus.Started);
        var replacement = scenario.State.CurrentView!;
        scenario.CompleteOld(oldWorkflowId, StartedAt.AddMinutes(3).AddSeconds(1));
        scenario.State.CurrentView!.WorkflowId.Should().Be(replacement.WorkflowId);
        scenario.State.CurrentView.WorkflowRevision.Should().Be(replacement.WorkflowRevision);
        scenario.State.Events.Should().HaveCount(2);
    }

    /// <summary>Given an available assessment, the workflow selects Trade Selection exactly once.</summary>
    [Fact]
    public void Available_assessment_continues_to_trade_selection()
    {
        var scenario = new Scenario(TimeFrameType.Daily);
        scenario.AdvanceToMarketCondition(StartedAt);

        scenario.CompleteMarketCondition(StartedAt.AddSeconds(20), AssessmentAvailability.Available);

        scenario.State.CurrentView.Should().BeEquivalentTo(new
        {
            Status = WorkflowStrategyMachineStatus.Started,
            Outcome = StrategyWorkflowOutcome.None,
            CurrentStage = StrategyWorkflowStage.TradeSelection
        });
    }

    /// <summary>Given unavailable required market data, Market Condition completes the workflow as NoTrade.</summary>
    [Fact]
    public void Unavailable_assessment_completes_without_trade_selection()
    {
        var scenario = new Scenario(TimeFrameType.Daily);
        scenario.AdvanceToMarketCondition(StartedAt);

        scenario.CompleteMarketCondition(StartedAt.AddSeconds(20), AssessmentAvailability.Unavailable);

        scenario.State.CurrentView.Should().BeEquivalentTo(new
        {
            Status = WorkflowStrategyMachineStatus.Completed,
            Outcome = StrategyWorkflowOutcome.NoTrade,
            CurrentStage = StrategyWorkflowStage.MarketCondition,
            StopReasonCode = "MC.ASSESSMENT.UNAVAILABLE"
        });
    }

    /// <summary>Given a typed Market Condition timeout, the workflow terminates and never selects Trade Selection.</summary>
    [Fact]
    public void Market_condition_timeout_is_terminal()
    {
        var scenario = new Scenario(TimeFrameType.Daily);
        scenario.AdvanceToMarketCondition(StartedAt);

        scenario.FailMarketCondition(StartedAt.AddSeconds(20), MarketConditionFailureCategory.Timeout);

        scenario.State.CurrentView.Should().BeEquivalentTo(new
        {
            Status = WorkflowStrategyMachineStatus.TimedOut,
            Outcome = StrategyWorkflowOutcome.TimedOut,
            CurrentStage = StrategyWorkflowStage.MarketCondition
        });
    }

    /// <summary>Given invalid mandatory input, Market Condition fails and never maps it to NoTrade.</summary>
    [Fact]
    public void Invalid_market_condition_input_is_an_operational_failure()
    {
        var scenario = new Scenario(TimeFrameType.Daily);
        scenario.AdvanceToMarketCondition(StartedAt);

        scenario.FailMarketCondition(StartedAt.AddSeconds(20),
            MarketConditionFailureCategory.RequiredInputInvalid);

        scenario.State.CurrentView.Should().BeEquivalentTo(new
        {
            Status = WorkflowStrategyMachineStatus.Failed,
            Outcome = StrategyWorkflowOutcome.PipelineFailed,
            CurrentStage = StrategyWorkflowStage.MarketCondition
        });
    }

    /// <summary>Given a result already expired at acceptance, the workflow times out and does not rerun it.</summary>
    [Fact]
    public void Expired_market_condition_completion_times_out_without_trade_selection()
    {
        var scenario = new Scenario(TimeFrameType.Daily);
        scenario.AdvanceToMarketCondition(StartedAt);

        scenario.CompleteMarketCondition(StartedAt.AddSeconds(20), AssessmentAvailability.Available,
            validForSeconds: -1);

        scenario.State.CurrentView.Should().BeEquivalentTo(new
        {
            Status = WorkflowStrategyMachineStatus.TimedOut,
            Outcome = StrategyWorkflowOutcome.TimedOut,
            CurrentStage = StrategyWorkflowStage.MarketCondition,
            StopReasonCode = MarketConditionReasonCodes.ResultExpired
        });
    }

    /// <summary>Given duplicate terminal data, the authoritative workflow revision changes at most once.</summary>
    [Fact]
    public void Duplicate_market_condition_terminal_changes_revision_once()
    {
        var scenario = new Scenario(TimeFrameType.Daily);
        scenario.AdvanceToMarketCondition(StartedAt);
        var command = scenario.CompleteMarketCondition(StartedAt.AddSeconds(20), AssessmentAvailability.Available);
        var revision = scenario.State.CurrentView!.WorkflowRevision;
        var eventCount = scenario.State.Events.Count;

        command.Execute(Context(StartedAt.AddSeconds(21)), scenario.State);

        scenario.State.CurrentView.WorkflowRevision.Should().Be(revision);
        scenario.State.Events.Should().HaveCount(eventCount);
    }

    sealed class Scenario
    {
        readonly IntrinsicTimeStrategyWorkflowEntityId _entityId;
        readonly RegimeDiscoveryParameterSet _parameters;
        readonly MarketConditionAssessmentParameterSet _marketConditionParameters;
        int _identity = 300;

        public Scenario(TimeFrameType period)
        {
            _entityId = IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId(
                "ES-202612", new DateOnly(2026, 8, 27), period));
            _parameters = RegimeDiscoveryParameterSet.CreateDefault(NextGuid(), NextGuid(), period);
            _marketConditionParameters = MarketConditionAssessmentParameterSet.CreateDefault(
                "ES.Bdd", period, NextGuid(), _parameters.ParameterSetId, _parameters.Version);
            WorkflowId = new StrategyWorkflowId(NextGuid());
        }

        public StrategyWorkflowId WorkflowId { get; private set; }
        public IntrinsicTimeStrategyWorkflowCommandState State { get; private set; } = new();

        public void Start(DateTime now)
            => ExecuteCommand(WorkflowId).Execute(Context(now), State);

        public void StartAndReload(DateTime now)
        {
            Start(now);
            var snapshot = State.Events.Cast<WorkflowStrategyStateUpdatedEvent>().Single();
            State = new IntrinsicTimeStrategyWorkflowCommandState();
            State.Apply(snapshot, addEvent: false).Should().BeTrue();
        }

        public void Complete(DateTime now)
            => CompleteOld(WorkflowId, now);

        public void AdvanceToMarketCondition(DateTime now)
        {
            StartAndReload(now);
            Complete(now.AddSeconds(10));
            var snapshot = State.Events.Cast<WorkflowStrategyStateUpdatedEvent>().Single();
            State = new IntrinsicTimeStrategyWorkflowCommandState();
            State.Apply(snapshot, addEvent: false).Should().BeTrue();
        }

        public CompleteMarketConditionCommand CompleteMarketCondition(DateTime now,
            AssessmentAvailability availability, int validForSeconds = 30)
        {
            var view = State.CurrentView!;
            var source = NextGuid();
            var evaluatedAt = validForSeconds < 0
                ? now.AddSeconds(validForSeconds - 1)
                : now;
            var regimeEnvelope = view.RegimeDiscovery.Result!;
            var result = new MarketConditionAssessmentResult
            {
                ResultId = source, CommandId = source, WorkflowId = view.WorkflowId, EntityId = view.EntityId,
                InputWorkflowRevision = view.WorkflowRevision,
                MarketProfileId = _marketConditionParameters.MarketProfileId, InstrumentRoot = "ES", TargetHorizon = _marketConditionParameters.TargetHorizon,
                ParameterSetId = _marketConditionParameters.ParameterSetId, ParameterSetVersion = _marketConditionParameters.Version,
                ParameterPayloadSha256 = view.AssessmentBinding!.PayloadSha256,
                RegimeResultId = regimeEnvelope.ResultId, RegimePayloadSha256 = regimeEnvelope.PayloadSha256,
                SnapshotId = NextGuid(), SnapshotSha256 = new string('A', 64), EvaluatedAtUtc = evaluatedAt,
                Assessment = new()
                {
                    Horizon = _marketConditionParameters.TargetHorizon, EvaluatedAtUtc = evaluatedAt,
                    RegimeResultId = regimeEnvelope.ResultId, RegimePayloadSha256 = regimeEnvelope.PayloadSha256,
                    Availability = availability,
                    ConditionType = availability == AssessmentAvailability.Available ? AssessmentCondition.Directional : null,
                    AssessmentConfidence = availability == AssessmentAvailability.Available ? 0.8m : null,
                    ValidUntilUtc = availability == AssessmentAvailability.Available ? now.AddSeconds(validForSeconds) : null,
                    UpstreamContext = MarketConditionAssessmentContracts.ReadRegime(regimeEnvelope, view).Decision, LimitationReasons = availability == AssessmentAvailability.Unavailable ? ["Missing feed"] : []
                },
                SummaryText = "BDD assessment"
            };
            MarketConditionAssessmentContracts.ValidateAcceptance(result, view, view.WorkflowRevision);
            var payload = MessagePackSerializer.Serialize(result);
            var command = new CompleteMarketConditionCommand
            {
                CommandId = NextGuid(),
                Subject = Subject(CompleteMarketConditionCommand.Verb),
                EntityId = _entityId,
                WorkflowId = view.WorkflowId,
                InputWorkflowRevision = view.WorkflowRevision,
                SourceEventId = source,
                Result = StrategyStageResultEnvelope.Create(source, nameof(MarketConditionAssessmentResult),
                    1, payload, evaluatedAt, evaluatedAt),
                CausationId = source,
                CompletedAtUtc = now
            };
            command.Execute(Context(now), State);
            return command;
        }

        public void FailMarketCondition(DateTime now, MarketConditionFailureCategory category)
        {
            var view = State.CurrentView!;
            var source = NextGuid();
            new FailMarketConditionCommand
            {
                CommandId = NextGuid(),
                Subject = Subject(FailMarketConditionCommand.Verb),
                EntityId = _entityId,
                WorkflowId = view.WorkflowId,
                InputWorkflowRevision = view.WorkflowRevision,
                SourceEventId = source,
                FailureCategory = category,
                Failure = new StrategyPipelineFailure
                {
                    ErrorCode = 24006,
                    ErrorMessage = "Market Condition timed out.",
                    ErrorType = "Command",
                    FailedAtUtc = now
                },
                CausationId = source,
                FailedAtUtc = now
            }.Execute(Context(now), State);
        }

        public void CompleteOld(StrategyWorkflowId workflowId, DateTime now)
        {
            var source = NextGuid();
            var command = new CompleteRegimeDiscoveryCommand
            {
                CommandId = source,
                Subject = Subject(CompleteRegimeDiscoveryCommand.Verb),
                EntityId = _entityId,
                WorkflowId = workflowId,
                InputWorkflowRevision = 1,
                SourceEventId = source,
                Result = RegimeEnvelope(source, workflowId, now),
                CausationId = source,
                CompletedAtUtc = now
            };
            command.Execute(Context(now), State);
        }

        StrategyStageResultEnvelope RegimeEnvelope(Guid source, StrategyWorkflowId workflowId, DateTime now)
        {
            var result = new RegimeDiscoveryResult
            {
                ResultId = source, WorkflowId = workflowId, EntityId = _entityId,
                TargetHorizon = _parameters.TargetHorizon, TriggerEventId = State.CurrentView!.TriggerEvent.Id,
                RegimeDiscoveryParameterSetId = _parameters.ParameterSetId, RegimeDiscoveryParameterSetVersion = _parameters.Version,
                ProducedAtUtc = now, MarketDataAsOfUtc = now, Decision = new() { IsComplete = true, Direction = RegimeDirection.Up }
            };
            return StrategyStageResultEnvelope.Create(source, nameof(RegimeDiscoveryResult), RegimeDiscoveryResult.CurrentSchemaVersion,
                MessagePackSerializer.Serialize(result), now, now);
        }

        public void Fail(DateTime now, string errorType)
        {
            var source = NextGuid();
            var command = new FailRegimeDiscoveryCommand
            {
                CommandId = source,
                Subject = Subject(FailRegimeDiscoveryCommand.Verb),
                EntityId = _entityId,
                WorkflowId = WorkflowId,
                InputWorkflowRevision = 1,
                SourceEventId = source,
                Failure = new StrategyPipelineFailure
                {
                    ErrorCode = errorType.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ? 23103 : 23102,
                    ErrorMessage = errorType,
                    ErrorType = errorType,
                    FailedAtUtc = now
                },
                CausationId = source,
                FailedAtUtc = now
            };
            command.Execute(Context(now), State);
        }

        public void Replace(DateTime now)
        {
            WorkflowId = new StrategyWorkflowId(NextGuid());
            ExecuteCommand(WorkflowId).Execute(Context(now), State);
        }

        ExecuteIntrinsicTimeStrategyWorkflowCommand ExecuteCommand(StrategyWorkflowId workflowId)
        {
            var triggerId = NextGuid();
            return new ExecuteIntrinsicTimeStrategyWorkflowCommand
            {
                CommandId = NextGuid(),
                Subject = Subject(ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb),
                EntityId = _entityId,
                ProposedWorkflowId = workflowId,
                TriggerEventId = triggerId,
                TriggerEvent = new FuturesItiSignalGeneratedEvent { Id = triggerId, EntityId = _entityId.ItiSignalEntityId },
                CorrelationId = NextGuid(),
                CausationId = triggerId,
                WorkflowDefinitionVersion = 1,
                RegimeDiscoveryParameterSet = _parameters,
                RegimeDiscoveryParameterPayloadSha256 = RegimeDiscoveryParameterPayload.ComputeSha256(_parameters),
                FundId = 1,
                AssessmentBinding = new() { Parameters = _marketConditionParameters,
                    PayloadSha256 = MarketConditionAssessmentHash.Parameters(_marketConditionParameters) }
            };
        }

        ActorSubject Subject(string verb)
            => new(ActorType.Command, ExecuteIntrinsicTimeStrategyWorkflowCommand.Actor, verb, _entityId.Format());

        Guid NextGuid() => Guid.Parse($"0198E212-3C00-7000-8000-{_identity++:D12}");
    }

    static FixedTimeProvider Time(DateTime value) => new(new DateTimeOffset(value, TimeSpan.Zero));

    static IIntrinsicTimeStrategyWorkflowCommandContext Context(DateTime now)
    {
        var context = Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();
        context.TimeProvider.Returns(Time(now));
        context.ExecutionOptions.Returns(new RegimeDiscoveryExecutionOptions
        {
            MaximumExecutionDuration = MaximumDuration
        });
        context.Logger.Returns(Substitute.For<ILogger<IntrinsicTimeStrategyWorkflowCommandActor>>());
        return context;
    }

    sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
