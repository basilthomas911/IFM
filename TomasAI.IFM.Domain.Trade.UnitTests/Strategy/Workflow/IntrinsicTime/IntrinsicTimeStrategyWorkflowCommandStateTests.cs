using FluentAssertions;
using MessagePack;
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

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Qualifies the RD-19D authoritative workflow snapshot transition table.</summary>
public sealed class IntrinsicTimeStrategyWorkflowCommandStateTests
{
    static readonly DateTime Now = new(2026, 8, 27, 14, 0, 0, DateTimeKind.Utc);
    static readonly TimeSpan MaximumDuration = TimeSpan.FromMinutes(2);
    static readonly IntrinsicTimeStrategyWorkflowEntityId EntityId =
        IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId(
            "ES-202612", new DateOnly(2026, 8, 27), TimeFrameType.Daily));
    static readonly StrategyWorkflowId WorkflowId = new(
        Guid.Parse("0198E212-3C00-7000-8000-000000000201"));

    /// <summary>Creates a valid Started snapshot for structural gate qualification tests.</summary>
    internal static WorkflowStrategyStateUpdatedEvent CreateStartedSnapshotForQualification()
    {
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        IntrinsicTimeStrategyWorkflowCommandActor.HandleStart(
            state, CreateStart(WorkflowId, TriggerId(99)), Time(Now), MaximumDuration);
        return LatestSnapshot(state);
    }

    /// <summary>Empty state accepts one Started snapshot with one fixed deadline.</summary>
    [Fact]
    public void Empty_start_commits_one_started_snapshot_with_fixed_deadline()
    {
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        var command = CreateStart(WorkflowId, TriggerId(1));

        IntrinsicTimeStrategyWorkflowCommandActor.HandleStart(
            state, command, Time(Now), MaximumDuration);

        state.Events.Should().ContainSingle();
        var snapshot = state.Events.Should().ContainSingle().Subject
            .Should().BeOfType<WorkflowStrategyStateUpdatedEvent>().Subject;
        snapshot.PreviousStatus.Should().Be(WorkflowStrategyMachineStatus.Empty);
        snapshot.State.Status.Should().Be(WorkflowStrategyMachineStatus.Started);
        snapshot.State.ExpiresAtUtc.Should().Be(Now.Add(MaximumDuration));
        snapshot.State.RegimeDiscovery.Should().BeEquivalentTo(new
        {
            ProcessingStatus = StrategyActorProcessingStatus.Processing,
            InputWorkflowRevision = 1L,
            ParameterSetId = command.RegimeDiscoveryParameterSet.ParameterSetId,
            ParameterSetVersion = command.RegimeDiscoveryParameterSet.Version,
            ParameterPayloadSha256 = command.RegimeDiscoveryParameterPayloadSha256,
            ExpiresAtUtc = (DateTime?)Now.Add(MaximumDuration)
        });
        state.CurrentView.Should().BeEquivalentTo(snapshot.State);
    }

    /// <summary>A terminal workflow is Free and accepts a different workflow execution.</summary>
    [Fact]
    public void Terminal_state_accepts_new_workflow()
    {
        var state = StartedState(Now.AddMinutes(-1));
        var failure = CreateFailureCommand(WorkflowId, 1, TriggerId(2), "ValidationFailure");
        IntrinsicTimeStrategyWorkflowCommandActor.HandleFailureForTest(state, failure, Time(Now));
        var terminal = state.CurrentView!;
        terminal.Status.Should().Be(WorkflowStrategyMachineStatus.Failed);
        var loaded = FromSnapshot(LatestSnapshot(state));
        var replacementId = new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000202"));

        IntrinsicTimeStrategyWorkflowCommandActor.HandleStart(
            loaded, CreateStart(replacementId, TriggerId(3)), Time(Now.AddSeconds(1)), MaximumDuration);

        loaded.Events.Should().ContainSingle();
        loaded.CurrentView!.WorkflowId.Should().Be(replacementId);
        loaded.CurrentView.Status.Should().Be(WorkflowStrategyMachineStatus.Started);
    }

    /// <summary>An unexpired Started snapshot remains Busy and appends nothing.</summary>
    [Fact]
    public void Unexpired_started_rejects_new_start_without_event()
    {
        var state = StartedState(Now);
        var replacement = new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000203"));

        IntrinsicTimeStrategyWorkflowCommandActor.HandleStart(
            state, CreateStart(replacement, TriggerId(4)), Time(Now.AddSeconds(30)), MaximumDuration);

        state.Events.Should().BeEmpty();
        state.CurrentView!.WorkflowId.Should().Be(WorkflowId);
    }

    /// <summary>Expired-old and Started-new snapshots are one pending PostgreSQL event batch.</summary>
    [Fact]
    public void Expired_started_closes_old_and_starts_new_in_one_event_batch()
    {
        var state = StartedState(Now.AddMinutes(-3));
        var replacement = new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000204"));

        IntrinsicTimeStrategyWorkflowCommandActor.HandleStart(
            state, CreateStart(replacement, TriggerId(5)), Time(Now), MaximumDuration);

        state.Events.Cast<WorkflowStrategyStateUpdatedEvent>().Select(value => value.State.Status)
            .Should().Equal(WorkflowStrategyMachineStatus.TimedOut, WorkflowStrategyMachineStatus.Started);
        state.Events.Cast<WorkflowStrategyStateUpdatedEvent>().Select(value => value.State.WorkflowId)
            .Should().Equal(WorkflowId, replacement);
        state.CurrentView!.WorkflowId.Should().Be(replacement);
    }

    /// <summary>A valid pre-deadline completion merges its result and selects only the next stage.</summary>
    [Fact]
    public void Completion_before_expiry_merges_result_and_selects_next_stage()
    {
        var state = StartedState(Now);
        var sourceId = TriggerId(6);
        var result = CreateResult(sourceId);

        IntrinsicTimeStrategyWorkflowCommandActor.HandleCompletionForTest(
            state, CreateCompletion(WorkflowId, 1, sourceId, result), Time(Now.AddSeconds(30)));

        state.Events.Should().ContainSingle();
        var view = state.CurrentView!;
        view.Status.Should().Be(WorkflowStrategyMachineStatus.Started);
        view.CurrentStage.Should().Be(StrategyWorkflowStage.MarketCondition);
        view.WorkflowRevision.Should().Be(2);
        view.RegimeDiscovery.Result!.ResultId.Should().Be(result.ResultId);
        view.RegimeDiscovery.SourceEventId.Should().Be(sourceId);
        view.MarketCondition.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Processing);
    }

    /// <summary>Equality at the deadline gives timeout precedence and discards the result.</summary>
    [Fact]
    public void Completion_exactly_at_expiry_times_out_without_result_merge()
    {
        var state = StartedState(Now);
        var sourceId = TriggerId(7);

        IntrinsicTimeStrategyWorkflowCommandActor.HandleCompletionForTest(
            state, CreateCompletion(WorkflowId, 1, sourceId, CreateResult(sourceId)),
            Time(Now.Add(MaximumDuration)));

        state.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.TimedOut);
        state.CurrentView.RegimeDiscovery.Result.Should().BeNull();
        state.CurrentView.RegimeDiscovery.SourceEventId.Should().Be(sourceId);
    }

    /// <summary>A non-timeout failure before expiry closes the workflow as Failed.</summary>
    [Fact]
    public void Failure_before_expiry_becomes_failed()
    {
        var state = StartedState(Now);
        var sourceId = TriggerId(8);

        IntrinsicTimeStrategyWorkflowCommandActor.HandleFailureForTest(
            state, CreateFailureCommand(WorkflowId, 1, sourceId, "DataUnavailable"),
            Time(Now.AddSeconds(10)));

        state.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.Failed);
        state.CurrentView.RegimeDiscovery.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Failed);
        state.CurrentView.RegimeDiscovery.SourceEventId.Should().Be(sourceId);
    }

    /// <summary>A timeout-classified failure wins even when receipt appears before the persisted deadline.</summary>
    [Fact]
    public void Timeout_classified_failure_has_precedence_before_deadline()
    {
        var state = StartedState(Now);
        var sourceId = TriggerId(9);

        IntrinsicTimeStrategyWorkflowCommandActor.HandleFailureForTest(
            state, CreateFailureCommand(WorkflowId, 1, sourceId, "RegimeDiscoveryTimedOut"),
            Time(Now.AddSeconds(10)));

        state.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.TimedOut);
        state.CurrentView.RegimeDiscovery.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.TimedOut);
    }

    /// <summary>Any failure received at or after the deadline becomes TimedOut.</summary>
    [Fact]
    public void Late_failure_becomes_timed_out()
    {
        var state = StartedState(Now);

        IntrinsicTimeStrategyWorkflowCommandActor.HandleFailureForTest(
            state, CreateFailureCommand(WorkflowId, 1, TriggerId(10), "DataUnavailable"),
            Time(Now.Add(MaximumDuration)));

        state.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.TimedOut);
    }

    /// <summary>Duplicate, stale-workflow, stale-revision, and wrong-stage inputs append nothing.</summary>
    [Fact]
    public void Duplicate_and_stale_terminal_inputs_are_no_ops()
    {
        var state = StartedState(Now);
        var sourceId = TriggerId(11);
        var completion = CreateCompletion(WorkflowId, 1, sourceId, CreateResult(sourceId));
        IntrinsicTimeStrategyWorkflowCommandActor.HandleCompletionForTest(
            state, completion, Time(Now.AddSeconds(10)));
        var committed = LatestSnapshot(state);
        var loaded = FromSnapshot(committed);

        IntrinsicTimeStrategyWorkflowCommandActor.HandleCompletionForTest(
            loaded, completion, Time(Now.AddSeconds(11)));
        IntrinsicTimeStrategyWorkflowCommandActor.HandleCompletionForTest(
            loaded, CreateCompletion(new StrategyWorkflowId(Guid.NewGuid()), 2, TriggerId(12), CreateResult(TriggerId(12))),
            Time(Now.AddSeconds(11)));
        IntrinsicTimeStrategyWorkflowCommandActor.HandleCompletionForTest(
            loaded, CreateCompletion(WorkflowId, 1, TriggerId(13), CreateResult(TriggerId(13))),
            Time(Now.AddSeconds(11)));

        loaded.Events.Should().BeEmpty();
        MessagePackSerializer.Serialize(loaded.CurrentView).Should()
            .Equal(MessagePackSerializer.Serialize(committed.State));
    }

    /// <summary>Restart applies exactly one latest snapshot and never reconstructs dispatch work.</summary>
    [Fact]
    public void Latest_snapshot_restart_reconstructs_exact_view_without_dispatch()
    {
        var state = StartedState(Now);
        var sourceId = TriggerId(14);
        IntrinsicTimeStrategyWorkflowCommandActor.HandleCompletionForTest(
            state, CreateCompletion(WorkflowId, 1, sourceId, CreateResult(sourceId)), Time(Now.AddSeconds(5)));
        var latest = LatestSnapshot(state);

        var replayed = FromSnapshot(latest);

        MessagePackSerializer.Serialize(replayed.CurrentView).Should()
            .Equal(MessagePackSerializer.Serialize(latest.State));
        replayed.Events.Should().BeEmpty();
        replayed.ActiveDispatchInstruction.Should().BeNull();
        replayed.HasAuthoritativeSnapshot.Should().BeTrue();
    }

    /// <summary>The runtime state rejects all legacy lifecycle events.</summary>
    [Fact]
    public void Legacy_event_cannot_be_applied_as_authoritative_state()
    {
        var state = new IntrinsicTimeStrategyWorkflowCommandState();

        state.Apply(new IntrinsicTimeStrategyWorkflowStartedEvent(), addEvent: false).Should().BeFalse();
        state.HasAuthoritativeSnapshot.Should().BeFalse();
    }

    /// <summary>Snapshot metadata mismatch fails closed.</summary>
    [Fact]
    public void Snapshot_metadata_mismatch_is_rejected()
    {
        var producer = new IntrinsicTimeStrategyWorkflowCommandState();
        IntrinsicTimeStrategyWorkflowCommandActor.HandleStart(
            producer, CreateStart(WorkflowId, TriggerId(101)), Time(Now), MaximumDuration);
        var valid = LatestSnapshot(producer);
        var state = new IntrinsicTimeStrategyWorkflowCommandState();

        state.Apply(valid with { WorkflowRevision = valid.WorkflowRevision + 1 }, addEvent: false)
            .Should().BeFalse();
    }

    static IntrinsicTimeStrategyWorkflowCommandState StartedState(DateTime startedAt)
    {
        var producer = new IntrinsicTimeStrategyWorkflowCommandState();
        IntrinsicTimeStrategyWorkflowCommandActor.HandleStart(
            producer, CreateStart(WorkflowId, TriggerId(100)), Time(startedAt), MaximumDuration);
        return FromSnapshot(LatestSnapshot(producer));
    }

    static IntrinsicTimeStrategyWorkflowCommandState FromSnapshot(WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(snapshot, addEvent: false).Should().BeTrue();
        return state;
    }

    static WorkflowStrategyStateUpdatedEvent LatestSnapshot(IntrinsicTimeStrategyWorkflowCommandState state)
        => state.Events.Cast<WorkflowStrategyStateUpdatedEvent>().Last();

    static StartIntrinsicTimeStrategyWorkflowCommand CreateStart(
        StrategyWorkflowId workflowId,
        Guid triggerId)
    {
        var parameterSet = RegimeDiscoveryParameterSet.CreateDefault(
            Guid.Parse("0198E212-3C00-7000-8000-000000000211"),
            Guid.Parse("0198E212-3C00-7000-8000-000000000212"),
            TimeFrameType.Daily,
            version: 3);
        return new StartIntrinsicTimeStrategyWorkflowCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = Subject(StartIntrinsicTimeStrategyWorkflowCommand.Verb),
            EntityId = EntityId,
            ProposedWorkflowId = workflowId,
            TriggerEventId = triggerId,
            TriggerEvent = new FuturesItiSignalGeneratedEvent { Id = triggerId, EntityId = EntityId.ItiSignalEntityId },
            CorrelationId = Guid.NewGuid(),
            CausationId = triggerId,
            RequestedAtUtc = Now,
            WorkflowDefinitionVersion = IntrinsicTimeStrategyWorkflowDefinition.Version,
            RegimeDiscoveryParameterSet = parameterSet,
            RegimeDiscoveryParameterPayloadSha256 = RegimeDiscoveryParameterPayload.ComputeSha256(parameterSet)
        };
    }

    static CompleteRegimeDiscoveryCommand CreateCompletion(
        StrategyWorkflowId workflowId,
        long revision,
        Guid sourceId,
        StrategyStageResultEnvelope result)
        => new()
        {
            CommandId = sourceId,
            Subject = Subject(CompleteRegimeDiscoveryCommand.Verb),
            EntityId = EntityId,
            WorkflowId = workflowId,
            InputWorkflowRevision = revision,
            SourceEventId = sourceId,
            Result = result,
            CorrelationId = Guid.NewGuid(),
            CausationId = sourceId,
            CompletedAtUtc = Now
        };

    static FailRegimeDiscoveryCommand CreateFailureCommand(
        StrategyWorkflowId workflowId,
        long revision,
        Guid sourceId,
        string errorType)
        => new()
        {
            CommandId = sourceId,
            Subject = Subject(FailRegimeDiscoveryCommand.Verb),
            EntityId = EntityId,
            WorkflowId = workflowId,
            InputWorkflowRevision = revision,
            SourceEventId = sourceId,
            Failure = new StrategyPipelineFailure
            {
                ErrorCode = errorType.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ? 23103 : 23102,
                ErrorMessage = errorType,
                ErrorType = errorType,
                FailedAtUtc = Now
            },
            CorrelationId = Guid.NewGuid(),
            CausationId = sourceId,
            FailedAtUtc = Now
        };

    static StrategyStageResultEnvelope CreateResult(Guid sourceId) => new()
    {
        ResultId = sourceId,
        ResultType = "RegimeDiscovery.Result",
        SchemaVersion = 1,
        ContentType = "application/x-msgpack",
        Payload = new byte[] { 0x91, 0x01 },
        PayloadSha256 = new string('A', 64),
        MarketDataAsOfUtc = Now,
        ProducedAtUtc = Now
    };

    static ActorSubject Subject(string verb)
        => new(ActorType.Command, StartIntrinsicTimeStrategyWorkflowCommand.Actor, verb, EntityId.Format());

    static Guid TriggerId(int suffix)
        => Guid.Parse($"0198E212-3C00-7000-8000-{suffix:D12}");

    static FixedTimeProvider Time(DateTime value) => new(new DateTimeOffset(value, TimeSpan.Zero));

    sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
