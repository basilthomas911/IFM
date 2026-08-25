using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Verifies ITSW-5 workflow command-state reduction, replay, and repository boundaries.</summary>
public sealed class IntrinsicTimeStrategyWorkflowCommandStateTests
{
    static readonly Guid WorkflowGuid = Guid.Parse("0198E212-3C00-7000-8000-000000000101");
    static readonly Guid TriggerGuid = Guid.Parse("0198E212-3C00-7000-8000-000000000102");
    static readonly Guid CommandGuid = Guid.Parse("0198E212-3C00-7000-8000-000000000103");
    static readonly Guid PipelineEventGuid = Guid.Parse("0198E212-3C00-7000-8000-000000000104");
    static readonly Guid PipelineCommandGuid = Guid.Parse("0198E212-3C00-7000-8000-000000000105");
    static readonly DateTime StartedAtUtc = new(2026, 8, 25, 18, 0, 0, DateTimeKind.Utc);
    static readonly DateTime CompletedAtUtc = StartedAtUtc.AddMinutes(2);
    static readonly IntrinsicTimeStrategyWorkflowEntityId EntityId =
        IntrinsicTimeStrategyWorkflowEntityId.Create(
            new FuturesItiSignalEntityId("ES-202609", new DateOnly(2026, 8, 25), TimeFrameType.Daily));
    static readonly StrategyWorkflowId WorkflowId = new(WorkflowGuid);

    /// <summary>Confirms an accepted start creates a running immutable workflow and bounded start metadata.</summary>
    [Fact]
    public void Start_accepted_creates_active_workflow_and_start_summary()
    {
        var trigger = CreateTriggerEvent();
        var state = new IntrinsicTimeStrategyWorkflowCommandState();

        state.Apply(CreateStartAccepted(trigger), addEvent: false).Should().BeTrue();

        state.HasActiveWorkflow.Should().BeTrue();
        state.ActiveWorkflow.Should().BeEquivalentTo(new
        {
            EntityId,
            WorkflowId,
            TriggerEventId = TriggerGuid,
            Status = StrategyWorkflowStatus.Running,
            Outcome = StrategyWorkflowOutcome.None,
            CurrentStage = StrategyWorkflowStage.RegimeDiscovery,
            WorkflowRevision = 1L,
            StartedAtUtc
        });
        state.ActiveWorkflow!.RegimeDiscovery.ProcessingStatus.Should()
            .Be(StrategyActorProcessingStatus.Processing);
        state.TotalStartRequests.Should().Be(1);
        state.AcceptedStartRequests.Should().Be(1);
        state.RejectedStartRequests.Should().Be(0);
        state.LastStartDecision.Should().Be(StrategyWorkflowStartDecision.Accepted);
        state.ActiveTriggerEvent.Should().NotBeSameAs(trigger);
        state.ActiveTriggerEvent!.FuturesItiSignal.Should().NotBeSameAs(trigger.FuturesItiSignal);
    }

    /// <summary>Confirms rejection changes only bounded attempt metadata and preserves the active revision.</summary>
    [Fact]
    public void Start_rejected_preserves_active_workflow_revision()
    {
        var state = CreateStartedState();
        var before = state.ActiveWorkflow;
        var rejectedTriggerId = Guid.Parse("0198E212-3C00-7000-8000-000000000106");

        state.Apply(new StrategyWorkflowStartRejectedEvent
        {
            EntityId = EntityId,
            EventId = 3,
            CommandId = Guid.Parse("0198E212-3C00-7000-8000-000000000107"),
            RequestedWorkflowId = new StrategyWorkflowId(
                Guid.Parse("0198E212-3C00-7000-8000-000000000108")),
            ActiveWorkflowId = WorkflowId,
            ActiveWorkflowRevision = 1,
            ActiveStage = StrategyWorkflowStage.RegimeDiscovery,
            TriggerEventId = rejectedTriggerId,
            ReasonCode = "WorkflowAlreadyExecuting",
            RejectedAtUtc = StartedAtUtc.AddSeconds(1)
        }, addEvent: false).Should().BeTrue();

        state.ActiveWorkflow.Should().BeEquivalentTo(before);
        state.ActiveWorkflow.Should().NotBeSameAs(before);
        state.ActiveWorkflow!.WorkflowRevision.Should().Be(1);
        state.TotalStartRequests.Should().Be(2);
        state.AcceptedStartRequests.Should().Be(1);
        state.RejectedStartRequests.Should().Be(1);
        state.LastStartDecision.Should().Be(StrategyWorkflowStartDecision.Rejected);
        state.LastTriggerEventId.Should().Be(rejectedTriggerId);
    }

    /// <summary>Confirms Started records the exact committed deterministic pipeline dispatch instruction.</summary>
    [Fact]
    public void Workflow_started_retains_committed_dispatch_instruction()
    {
        var trigger = CreateTriggerEvent();
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(CreateStartAccepted(trigger), addEvent: false);
        var workflow = state.ActiveWorkflow!;

        state.Apply(CreateWorkflowStarted(workflow, trigger), addEvent: false).Should().BeTrue();

        state.ActiveDispatchInstruction.Should().NotBeNull();
        var dispatch = state.ActiveDispatchInstruction!;
        dispatch.Stage.Should().Be(StrategyWorkflowStage.RegimeDiscovery);
        dispatch.ActorType.Should().Be(ActorType.Command);
        dispatch.ActorName.Should().Be("RegimeDiscoveryPipelineCommand");
        dispatch.BoundedContext.Should().Be(BoundedContextName.RegimeDiscoveryPipelineBoundedContext);
        dispatch.CommandId.Should().Be(PipelineCommandGuid);
        dispatch.WorkflowState.Should().NotBeSameAs(workflow);
        dispatch.TriggerEvent.Should().NotBeSameAs(trigger);
    }

    /// <summary>Confirms result, continuation, and next-stage events replace rather than mutate snapshot graphs.</summary>
    [Fact]
    public void Stage_progression_preserves_previous_snapshot_instances()
    {
        var state = CreateStartedState();
        var beforeResult = state.ActiveWorkflow!;
        var result = CreateResult();

        state.Apply(new StrategyWorkflowRegimeDiscoveryResultRecordedEvent
        {
            EntityId = EntityId,
            EventId = 3,
            WorkflowId = WorkflowId,
            WorkflowRevision = 2,
            Stage = StrategyWorkflowStage.RegimeDiscovery,
            SourceEventId = PipelineEventGuid,
            Result = result,
            RecordedAtUtc = CompletedAtUtc
        }, addEvent: false).Should().BeTrue();
        var beforeContinuation = state.ActiveWorkflow!;

        state.Apply(new StrategyWorkflowRegimeDiscoveryContinuationEvaluatedEvent
        {
            EntityId = EntityId,
            EventId = 4,
            WorkflowId = WorkflowId,
            WorkflowRevision = 2,
            Stage = StrategyWorkflowStage.RegimeDiscovery,
            Decision = StrategyWorkflowContinuationDecision.Proceed,
            RuleSetId = "SkeletonProceedOnValidResult",
            RuleSetVersion = 1,
            ReasonCodes = ["VALID_RESULT"],
            EvaluatedAtUtc = CompletedAtUtc
        }, addEvent: false).Should().BeTrue();
        var continuedInput = state.ActiveWorkflow! with
        {
            CurrentStage = StrategyWorkflowStage.MarketCondition,
            MarketCondition = new StrategyWorkflowStageState()
        };

        state.Apply(CreateWorkflowContinued(continuedInput), addEvent: false).Should().BeTrue();

        beforeResult.RegimeDiscovery.Result.Should().BeNull();
        beforeResult.WorkflowRevision.Should().Be(1);
        beforeContinuation.RegimeDiscovery.Result.Should().NotBeNull();
        beforeContinuation.RegimeDiscovery.ContinuationDecision.Should()
            .Be(StrategyWorkflowContinuationDecision.None);
        state.ActiveWorkflow!.RegimeDiscovery.ContinuationDecision.Should()
            .Be(StrategyWorkflowContinuationDecision.Proceed);
        state.ActiveWorkflow.CurrentStage.Should().Be(StrategyWorkflowStage.MarketCondition);
        state.ActiveWorkflow.MarketCondition.ProcessingStatus.Should()
            .Be(StrategyActorProcessingStatus.Processing);
        state.HasProcessedPipelineEvent(PipelineEventGuid).Should().BeTrue();
        state.ActiveDispatchInstruction!.Stage.Should().Be(StrategyWorkflowStage.MarketCondition);
    }

    /// <summary>Confirms a pipeline failure and stop create terminal state and release active execution metadata.</summary>
    [Fact]
    public void Pipeline_failure_stops_workflow_and_allows_a_new_trigger()
    {
        var state = CreateStartedState();
        var failure = CreateFailure();

        state.Apply(new StrategyWorkflowRegimeDiscoveryFailedEvent
        {
            EntityId = EntityId,
            EventId = 3,
            WorkflowId = WorkflowId,
            WorkflowRevision = 2,
            Stage = StrategyWorkflowStage.RegimeDiscovery,
            SourceEventId = PipelineEventGuid,
            Failure = failure,
            FailedAtUtc = CompletedAtUtc
        }, addEvent: false);
        state.Apply(CreateStopped(
            StrategyWorkflowOutcome.PipelineFailed,
            "REGIME_DISCOVERY_FAILED"), addEvent: false);

        state.HasActiveWorkflow.Should().BeFalse();
        state.ActiveWorkflow.Should().BeNull();
        state.LatestWorkflow!.Status.Should().Be(StrategyWorkflowStatus.Stopped);
        state.LatestWorkflow.Outcome.Should().Be(StrategyWorkflowOutcome.PipelineFailed);
        state.LatestWorkflow.RegimeDiscovery.ProcessingStatus.Should()
            .Be(StrategyActorProcessingStatus.Failed);
        state.LatestWorkflow.RegimeDiscovery.Failure.Should().Be(failure);
        state.ActiveTriggerEvent.Should().BeNull();
        state.ActiveDispatchInstruction.Should().BeNull();
        state.HasProcessedPipelineEvent(PipelineEventGuid).Should().BeTrue();
        state.CanAcceptStart(Guid.NewGuid()).Should().BeTrue();
    }

    /// <summary>Confirms timeout identity and processing status survive terminal replay.</summary>
    [Fact]
    public void Timeout_stops_workflow_and_retains_bounded_timeout_identity()
    {
        var state = CreateStartedState();
        var timeoutId = Guid.Parse("0198E212-3C00-7000-8000-000000000109");

        state.Apply(new StrategyWorkflowRegimeDiscoveryTimedOutEvent
        {
            EntityId = EntityId,
            EventId = 3,
            WorkflowId = WorkflowId,
            WorkflowRevision = 2,
            Stage = StrategyWorkflowStage.RegimeDiscovery,
            TimeoutId = timeoutId,
            TimedOutAtUtc = CompletedAtUtc
        }, addEvent: false);
        state.Apply(CreateStopped(StrategyWorkflowOutcome.TimedOut, "PIPELINE_TIMEOUT"), addEvent: false);

        state.LatestWorkflow!.RegimeDiscovery.ProcessingStatus.Should()
            .Be(StrategyActorProcessingStatus.TimedOut);
        state.LatestWorkflow.RegimeDiscovery.FailedAtUtc.Should().Be(CompletedAtUtc);
        state.HasProcessedTimeout(timeoutId).Should().BeTrue();
    }

    /// <summary>Confirms successful completion preserves the Risk Management result and releases active metadata.</summary>
    [Fact]
    public void Completed_event_creates_terminal_completed_snapshot()
    {
        var state = CreateStartedState();
        var riskState = new StrategyWorkflowStageState
        {
            ProcessingStatus = StrategyActorProcessingStatus.Completed,
            CompletedAtUtc = CompletedAtUtc,
            Result = CreateResult("RiskManagement.Approval")
        };
        var workflow = state.ActiveWorkflow! with
        {
            CurrentStage = StrategyWorkflowStage.RiskManagement,
            WorkflowRevision = 5,
            RiskManagement = riskState
        };
        state.Apply(CreateWorkflowContinued(workflow, StrategyWorkflowStage.RiskManagement), addEvent: false);

        state.Apply(new IntrinsicTimeStrategyWorkflowCompletedEvent
        {
            EntityId = EntityId,
            EventId = 8,
            WorkflowId = WorkflowId,
            WorkflowRevision = 6,
            Stage = StrategyWorkflowStage.RiskManagement,
            CompletedAtUtc = CompletedAtUtc
        }, addEvent: false).Should().BeTrue();

        state.HasActiveWorkflow.Should().BeFalse();
        state.LatestWorkflow!.Status.Should().Be(StrategyWorkflowStatus.Completed);
        state.LatestWorkflow.Outcome.Should().Be(StrategyWorkflowOutcome.Completed);
        state.LatestWorkflow.TerminalAtUtc.Should().Be(CompletedAtUtc);
        state.LatestWorkflow.RiskManagement.Result!.ResultType.Should().Be("RiskManagement.Approval");
        state.ActiveDispatchInstruction.Should().BeNull();
    }

    /// <summary>Confirms replay reconstructs the same state without leaving replayed events pending for persistence.</summary>
    [Fact]
    public void Replay_reconstructs_exact_state_without_pending_events()
    {
        var trigger = CreateTriggerEvent();
        var accepted = CreateStartAccepted(trigger);
        var started = CreateWorkflowStarted(CreateInitialWorkflow(), trigger);
        var result = new StrategyWorkflowRegimeDiscoveryResultRecordedEvent
        {
            EntityId = EntityId,
            EventId = 3,
            WorkflowId = WorkflowId,
            WorkflowRevision = 2,
            Stage = StrategyWorkflowStage.RegimeDiscovery,
            SourceEventId = PipelineEventGuid,
            Result = CreateResult(),
            RecordedAtUtc = CompletedAtUtc
        };
        IEvent[] events = [accepted, started, result];
        var first = new IntrinsicTimeStrategyWorkflowCommandState();
        var replayed = new IntrinsicTimeStrategyWorkflowCommandState();
        foreach (var @event in events)
            first.Apply(@event, addEvent: false);

        replayed.ReplayEvents(events);

        MessagePackSerializer.Serialize(replayed.LatestWorkflow).Should()
            .Equal(MessagePackSerializer.Serialize(first.LatestWorkflow));
        replayed.ActiveTriggerEvent.Should().BeEquivalentTo(first.ActiveTriggerEvent);
        replayed.ActiveDispatchInstruction.Should().BeEquivalentTo(first.ActiveDispatchInstruction);
        replayed.Events.Should().BeEmpty();
        replayed.Updated.Should().BeFalse();
        replayed.AppliedEntityEventCount.Should().Be(3);
        replayed.LastPersistedEventId.Should().Be(3);
    }

    /// <summary>Confirms every ITSW-3 workflow-owned event contract is explicitly recognized by the reducer.</summary>
    [Fact]
    public void Reducer_explicitly_supports_all_workflow_owned_events()
    {
        var eventTypes = typeof(StrategyWorkflowStartAcceptedEvent).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.Namespace == typeof(StrategyWorkflowStartAcceptedEvent).Namespace)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        eventTypes.Should().HaveCount(26);
        foreach (var eventType in eventTypes)
        {
            var state = eventType == typeof(StrategyWorkflowStartAcceptedEvent)
                || eventType == typeof(StrategyWorkflowStartRejectedEvent)
                    ? new IntrinsicTimeStrategyWorkflowCommandState()
                    : CreateStartedState();
            var @event = (IEvent)CreatePopulatedEvent(eventType);

            state.Apply(@event, addEvent: false).Should().BeTrue(eventType.Name);
        }
    }

    /// <summary>Confirms unrelated pipeline lifecycle events are not mistaken for workflow-owned state events.</summary>
    [Fact]
    public void Reducer_rejects_unknown_event_without_changing_state()
    {
        var state = CreateStartedState();
        var before = state.LatestWorkflow;
        var beforeCount = state.AppliedEntityEventCount;

        state.Apply(new RegimeDiscoveryPipelineProcessingEvent
        {
            EntityId = EntityId,
            WorkflowId = WorkflowId,
            PipelineStage = StrategyWorkflowStage.RegimeDiscovery,
            ProcessingAtUtc = StartedAtUtc
        }, addEvent: false).Should().BeFalse();

        state.LatestWorkflow.Should().BeEquivalentTo(before);
        state.AppliedEntityEventCount.Should().Be(beforeCount);
    }

    /// <summary>Confirms single-flight and immediate duplicate eligibility derive from persisted state only.</summary>
    [Fact]
    public void Start_eligibility_enforces_single_flight_and_latest_trigger_deduplication()
    {
        var state = CreateStartedState();

        state.CanAcceptStart(Guid.NewGuid()).Should().BeFalse("a workflow is already running");
        state.IsDuplicateTrigger(TriggerGuid).Should().BeTrue();
        state.Apply(CreateStopped(StrategyWorkflowOutcome.Cancelled, "TEST_CANCEL"), addEvent: false);

        state.CanAcceptStart(TriggerGuid).Should().BeFalse("the latest trigger is already persisted");
        state.CanAcceptStart(Guid.NewGuid()).Should().BeTrue();
    }

    /// <summary>Confirms the ITSW-5 repository uses the standard full-stream event-source boundary.</summary>
    [Fact]
    public void Repository_implements_standard_event_source_contract_without_projector_dependency()
    {
        var type = typeof(IntrinsicTimeStrategyWorkflowStateRepository);

        type.IsSealed.Should().BeTrue();
        type.Should().BeDerivedFrom<BaseEventSourceActorRepository>();
        type.Should().Implement<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>();
        type.GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType.Name).Should().Equal(
            "IEventSourceActorStateFactory",
            "IEventSourceActorDbContext",
            "IActorService",
            "ILogger`1");
        type.GetConstructors().Single().GetParameters().Should()
            .NotContain(parameter => parameter.ParameterType.Name.StartsWith("IEventProjector", StringComparison.Ordinal));
    }

    static IntrinsicTimeStrategyWorkflowCommandState CreateStartedState()
    {
        var trigger = CreateTriggerEvent();
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(CreateStartAccepted(trigger), addEvent: false);
        state.Apply(CreateWorkflowStarted(state.ActiveWorkflow!, trigger), addEvent: false);
        return state;
    }

    static StrategyWorkflowStartAcceptedEvent CreateStartAccepted(FuturesItiSignalGeneratedEvent trigger)
        => new()
        {
            EntityId = EntityId,
            Id = Guid.Parse("0198E212-3C00-7000-8000-000000000110"),
            EventId = 1,
            CommandId = CommandGuid,
            WorkflowId = WorkflowId,
            WorkflowRevision = 1,
            CorrelationId = WorkflowGuid,
            CausationId = TriggerGuid,
            Stage = StrategyWorkflowStage.RegimeDiscovery,
            TriggerEventId = TriggerGuid,
            TriggerEvent = trigger,
            WorkflowDefinitionVersion = IntrinsicTimeStrategyWorkflowDefinition.Version,
            StartedAtUtc = StartedAtUtc
        };

    static IntrinsicTimeStrategyWorkflowStartedEvent CreateWorkflowStarted(
        IntrinsicTimeStrategyWorkflowState workflow,
        FuturesItiSignalGeneratedEvent trigger)
        => new()
        {
            EntityId = EntityId,
            Id = Guid.Parse("0198E212-3C00-7000-8000-000000000111"),
            EventId = 2,
            WorkflowId = WorkflowId,
            WorkflowRevision = 1,
            CorrelationId = WorkflowGuid,
            CausationId = TriggerGuid,
            NextPipelineStage = StrategyWorkflowStage.RegimeDiscovery,
            NextPipelineActorType = ActorType.Command,
            NextPipelineActorName = "RegimeDiscoveryPipelineCommand",
            NextPipelineBoundedContext = BoundedContextName.RegimeDiscoveryPipelineBoundedContext,
            NextPipelineCommandId = PipelineCommandGuid,
            WorkflowState = workflow,
            TriggerEvent = trigger,
            RequestedAtUtc = StartedAtUtc,
            ExpectedCompletionAtUtc = StartedAtUtc.AddMinutes(5),
            StartedAtUtc = StartedAtUtc
        };

    static IntrinsicTimeStrategyWorkflowContinuedEvent CreateWorkflowContinued(
        IntrinsicTimeStrategyWorkflowState workflow,
        StrategyWorkflowStage nextStage = StrategyWorkflowStage.MarketCondition)
        => new()
        {
            EntityId = EntityId,
            Id = Guid.Parse("0198E212-3C00-7000-8000-000000000112"),
            EventId = 5,
            WorkflowId = WorkflowId,
            WorkflowRevision = workflow.WorkflowRevision,
            CorrelationId = WorkflowGuid,
            CausationId = PipelineEventGuid,
            CompletedPipelineStage = StrategyWorkflowStage.RegimeDiscovery,
            NextPipelineStage = nextStage,
            NextPipelineActorType = ActorType.Command,
            NextPipelineActorName = $"{nextStage}PipelineCommand",
            NextPipelineBoundedContext = BoundedContextName.MarketConditionPipelineBoundedContext,
            NextPipelineCommandId = Guid.Parse("0198E212-3C00-7000-8000-000000000113"),
            WorkflowState = workflow,
            TriggerEvent = CreateTriggerEvent(),
            ContinuationRuleSetId = "SkeletonProceedOnValidResult",
            ContinuationRuleSetVersion = 1,
            ContinuationReasonCodes = ["VALID_RESULT"],
            RequestedAtUtc = CompletedAtUtc,
            ExpectedCompletionAtUtc = CompletedAtUtc.AddMinutes(5),
            ContinuedAtUtc = CompletedAtUtc
        };

    static IntrinsicTimeStrategyWorkflowStoppedEvent CreateStopped(
        StrategyWorkflowOutcome outcome,
        string reasonCode)
        => new()
        {
            EntityId = EntityId,
            EventId = 4,
            WorkflowId = WorkflowId,
            WorkflowRevision = 2,
            CorrelationId = WorkflowGuid,
            CausationId = PipelineEventGuid,
            Stage = StrategyWorkflowStage.RegimeDiscovery,
            Outcome = outcome,
            ReasonCode = reasonCode,
            StoppedAtUtc = CompletedAtUtc
        };

    static IntrinsicTimeStrategyWorkflowState CreateInitialWorkflow()
        => new()
        {
            EntityId = EntityId,
            WorkflowId = WorkflowId,
            TriggerEventId = TriggerGuid,
            CorrelationId = WorkflowGuid,
            WorkflowDefinitionVersion = IntrinsicTimeStrategyWorkflowDefinition.Version,
            Status = StrategyWorkflowStatus.Running,
            CurrentStage = StrategyWorkflowStage.RegimeDiscovery,
            WorkflowRevision = 1,
            StartedAtUtc = StartedAtUtc,
            RegimeDiscovery = new StrategyWorkflowStageState
            {
                ProcessingStatus = StrategyActorProcessingStatus.Processing,
                StartedAtUtc = StartedAtUtc
            }
        };

    static FuturesItiSignalGeneratedEvent CreateTriggerEvent()
    {
        var signalEntityId = new FuturesItiSignalEntityId(
            "ES-202609",
            new DateOnly(2026, 8, 25),
            TimeFrameType.Daily);
        return new FuturesItiSignalGeneratedEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesItiSignalGeneratedEvent.Actor,
                FuturesItiSignalGeneratedEvent.Verb,
                signalEntityId.Format()),
            Id = TriggerGuid,
            EntityId = signalEntityId,
            EventId = 41,
            CommandId = Guid.Parse("0198E212-3C00-7000-8000-000000000114"),
            AggregateId = signalEntityId.Format(),
            EventSource = "FuturesItiSignalCommandActor",
            ReceivedOn = StartedAtUtc,
            FuturesItiSignal = new FuturesItiSignalV2ReadModel
            {
                ContractId = "ES-202609",
                ValueDate = new DateOnly(2026, 8, 25),
                TimeFrameStartValueDate = new DateOnly(2026, 8, 25),
                TimePeriod = TimeFrameType.Daily,
                IntrinsicTime = StartedAtUtc,
                IntrinsicPrice = 6432.25
            },
            CreatedOn = StartedAtUtc,
            CreatedBy = "itsw-5-test",
            VixFuturesPrice = 17.25
        };
    }

    static StrategyStageResultEnvelope CreateResult(string resultType = "RegimeDiscovery.Result")
        => StrategyStageResultEnvelope.Create(
            Guid.Parse("0198E212-3C00-7000-8000-000000000115"),
            resultType,
            1,
            new byte[] { 0x91, 0x01 },
            StartedAtUtc.AddMinutes(1),
            CompletedAtUtc);

    static StrategyPipelineFailure CreateFailure()
        => new()
        {
            ErrorCode = 4201,
            ErrorMessage = "Regime discovery failed.",
            ErrorType = "RegimeDiscoveryUnavailable",
            ErrorData = "source=unit-test",
            FailedAtUtc = CompletedAtUtc
        };

    static object CreatePopulatedEvent(Type type)
    {
        var constructor = type.GetConstructors().Single(candidate =>
            candidate.GetCustomAttribute<SerializationConstructorAttribute>() is not null);
        return constructor.Invoke(constructor.GetParameters()
            .Select(parameter => CreateSampleValue(parameter.ParameterType, parameter.Name!))
            .ToArray());
    }

    static object? CreateSampleValue(Type type, string parameterName)
    {
        if (type == typeof(Guid))
            return parameterName.Contains("trigger", StringComparison.OrdinalIgnoreCase)
                ? TriggerGuid
                : PipelineEventGuid;
        if (type == typeof(ActorSubject))
            return new ActorSubject(ActorType.Event, "IntrinsicTimeStrategyWorkflow", "Test", EntityId.Format());
        if (type == typeof(IntrinsicTimeStrategyWorkflowEntityId))
            return EntityId;
        if (type == typeof(long))
            return parameterName.Contains("revision", StringComparison.OrdinalIgnoreCase) ? 2L : 3L;
        if (type == typeof(int))
            return 1;
        if (type == typeof(string))
            return parameterName.Contains("actorName", StringComparison.OrdinalIgnoreCase)
                ? "RegimeDiscoveryPipelineCommand"
                : $"test-{parameterName}";
        if (type == typeof(DateTime))
            return CompletedAtUtc;
        if (type == typeof(DateTime?))
            return CompletedAtUtc.AddMinutes(5);
        if (type == typeof(StrategyWorkflowId))
            return WorkflowId;
        if (type == typeof(StrategyWorkflowStage))
            return StrategyWorkflowStage.RegimeDiscovery;
        if (type == typeof(StrategyWorkflowStartDecision))
            return StrategyWorkflowStartDecision.Accepted;
        if (type == typeof(StrategyWorkflowContinuationDecision))
            return StrategyWorkflowContinuationDecision.Proceed;
        if (type == typeof(StrategyWorkflowOutcome))
            return StrategyWorkflowOutcome.Cancelled;
        if (type == typeof(StrategyStageResultEnvelope))
            return CreateResult();
        if (type == typeof(StrategyPipelineFailure))
            return CreateFailure();
        if (type == typeof(FuturesItiSignalGeneratedEvent))
            return CreateTriggerEvent();
        if (type == typeof(IntrinsicTimeStrategyWorkflowState))
            return CreateInitialWorkflow() with { WorkflowRevision = 2 };
        if (type == typeof(ActorType))
            return ActorType.Command;
        if (type == typeof(BoundedContextName))
            return BoundedContextName.RegimeDiscoveryPipelineBoundedContext;
        if (type == typeof(string[]))
            return new[] { "VALID_RESULT" };

        throw new InvalidOperationException(
            $"No ITSW-5 event-test value is defined for {type.FullName} ({parameterName}).");
    }
}
