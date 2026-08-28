using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
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

    sealed class Scenario
    {
        readonly IntrinsicTimeStrategyWorkflowEntityId _entityId;
        readonly RegimeDiscoveryParameterSet _parameters;
        int _identity = 300;

        public Scenario(TimeFrameType period)
        {
            _entityId = IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId(
                "ES-202612", new DateOnly(2026, 8, 27), period));
            _parameters = RegimeDiscoveryParameterSet.CreateDefault(NextGuid(), NextGuid(), period);
            WorkflowId = new StrategyWorkflowId(NextGuid());
        }

        public StrategyWorkflowId WorkflowId { get; private set; }
        public IntrinsicTimeStrategyWorkflowCommandState State { get; private set; } = new();

        public void Start(DateTime now)
            => IntrinsicTimeStrategyWorkflowCommandActor.HandleExecute(
                State, ExecuteCommand(WorkflowId), Time(now), MaximumDuration);

        public void StartAndReload(DateTime now)
        {
            Start(now);
            var snapshot = State.Events.Cast<WorkflowStrategyStateUpdatedEvent>().Single();
            State = new IntrinsicTimeStrategyWorkflowCommandState();
            State.Apply(snapshot, addEvent: false).Should().BeTrue();
        }

        public void Complete(DateTime now)
            => CompleteOld(WorkflowId, now);

        public void CompleteOld(StrategyWorkflowId workflowId, DateTime now)
        {
            var source = NextGuid();
            IntrinsicTimeStrategyWorkflowCommandActor.HandleCompletionForTest(State,
                new CompleteRegimeDiscoveryCommand
                {
                    CommandId = source,
                    Subject = Subject(CompleteRegimeDiscoveryCommand.Verb),
                    EntityId = _entityId,
                    WorkflowId = workflowId,
                    InputWorkflowRevision = 1,
                    SourceEventId = source,
                    Result = new StrategyStageResultEnvelope
                    {
                        ResultId = source,
                        ResultType = "RegimeDiscovery.Result",
                        SchemaVersion = 1,
                        ContentType = "application/x-msgpack",
                        Payload = new byte[] { 0x91, 0x01 },
                        PayloadSha256 = new string('A', 64),
                        MarketDataAsOfUtc = now,
                        ProducedAtUtc = now
                    },
                    CausationId = source,
                    CompletedAtUtc = now
                }, Time(now));
        }

        public void Fail(DateTime now, string errorType)
        {
            var source = NextGuid();
            IntrinsicTimeStrategyWorkflowCommandActor.HandleFailureForTest(State,
                new FailRegimeDiscoveryCommand
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
                }, Time(now));
        }

        public void Replace(DateTime now)
        {
            WorkflowId = new StrategyWorkflowId(NextGuid());
            IntrinsicTimeStrategyWorkflowCommandActor.HandleExecute(
                State, ExecuteCommand(WorkflowId), Time(now), MaximumDuration);
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
                RegimeDiscoveryParameterPayloadSha256 = RegimeDiscoveryParameterPayload.ComputeSha256(_parameters)
            };
        }

        ActorSubject Subject(string verb)
            => new(ActorType.Command, ExecuteIntrinsicTimeStrategyWorkflowCommand.Actor, verb, _entityId.Format());

        Guid NextGuid() => Guid.Parse($"0198E212-3C00-7000-8000-{_identity++:D12}");
    }

    static FixedTimeProvider Time(DateTime value) => new(new DateTimeOffset(value, TimeSpan.Zero));

    sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
