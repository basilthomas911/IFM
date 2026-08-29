using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;

/// <summary>
/// Holds the private event-sourced state owned by the Intrinsic Time Strategy Workflow Command actor.
/// </summary>
/// <remarks>
/// Every supported state-update event replaces the current workflow view with a newly constructed immutable record
/// graph. Runtime recovery applies only the latest authoritative snapshot; legacy workflow events are deliberately
/// rejected by the repository instead of being replayed into live state.
/// </remarks>
public sealed class IntrinsicTimeStrategyWorkflowCommandState
    : BaseEventSourceActorState<IntrinsicTimeStrategyWorkflowCommandState>,
      IEventSourceActorState<IntrinsicTimeStrategyWorkflowCommandState>
{
    IntrinsicTimeStrategyWorkflowView? _currentView;
    IntrinsicTimeStrategyWorkflowState? _latestWorkflow;
    FuturesItiSignalGeneratedEvent? _activeTriggerEvent;
    ImmutableDictionary<StrategyWorkflowStage, Guid> _processedPipelineEventIds
        = ImmutableDictionary<StrategyWorkflowStage, Guid>.Empty;
    ImmutableDictionary<StrategyWorkflowStage, StrategyPipelineResultIdentity> _processedPipelineResults
        = ImmutableDictionary<StrategyWorkflowStage, StrategyPipelineResultIdentity>.Empty;
    ImmutableDictionary<StrategyWorkflowStage, Guid> _processedTimeoutIds
        = ImmutableDictionary<StrategyWorkflowStage, Guid>.Empty;

    /// <summary>Gets or sets the actor-thread identity assigned by the actor framework.</summary>
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>Gets the workflow entity associated with the replayed stream.</summary>
    public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; private set; } = new();

    /// <summary>Gets whether an authoritative state-update snapshot has been applied.</summary>
    public bool HasAuthoritativeSnapshot => _currentView is not null;

    /// <summary>Gets a defensive copy of the latest authoritative workflow view.</summary>
    public IntrinsicTimeStrategyWorkflowView? CurrentView
        => _currentView is null ? null : CloneView(_currentView);

    /// <summary>Gets the PostgreSQL stream version observed when this state was loaded.</summary>
    public long PersistedStreamVersion { get; private set; }

    /// <summary>Gets a value indicating whether this entity currently has a running workflow.</summary>
    public bool HasActiveWorkflow => _latestWorkflow is { Status: StrategyWorkflowStatus.Running };

    /// <summary>Gets a deep immutable copy of the active workflow, or <see langword="null"/> when terminal or empty.</summary>
    public IntrinsicTimeStrategyWorkflowState? ActiveWorkflow
        => HasActiveWorkflow ? CloneWorkflow(_latestWorkflow!) : null;

    /// <summary>Gets a deep immutable copy of the most recently reconstructed workflow, including terminal state.</summary>
    public IntrinsicTimeStrategyWorkflowState? LatestWorkflow
        => _latestWorkflow is null ? null : CloneWorkflow(_latestWorkflow);

    /// <summary>Gets the total number of persisted start decisions applied to this state.</summary>
    public long TotalStartRequests { get; private set; }

    /// <summary>Gets the number of accepted start decisions applied to this state.</summary>
    public long AcceptedStartRequests { get; private set; }

    /// <summary>Gets the number of rejected start decisions applied to this state.</summary>
    public long RejectedStartRequests { get; private set; }

    /// <summary>Gets the command identity associated with the latest start decision.</summary>
    public Guid? LastStartCommandId { get; private set; }

    /// <summary>Gets the trigger-event identity associated with the latest start decision.</summary>
    public Guid? LastTriggerEventId { get; private set; }

    /// <summary>Gets the workflow identity proposed by the latest start decision.</summary>
    public StrategyWorkflowId? LastRequestedWorkflowId { get; private set; }

    /// <summary>Gets the latest persisted start decision.</summary>
    public StrategyWorkflowStartDecision LastStartDecision { get; private set; }

    /// <summary>Gets the timestamp associated with the latest start decision.</summary>
    public DateTime? LastStartRequestedAtUtc { get; private set; }

    /// <summary>Gets the number of supported workflow events applied to this state instance.</summary>
    public long AppliedEntityEventCount { get; private set; }

    /// <summary>Gets the greatest persisted event-stream identity observed during application or replay.</summary>
    public long LastPersistedEventId { get; private set; }

    /// <summary>Determines whether the supplied trigger is the latest persisted start request.</summary>
    /// <param name="triggerEventId">Trigger event identity to inspect.</param>
    /// <returns><see langword="true"/> when the identity matches the latest start decision.</returns>
    public bool IsDuplicateTrigger(Guid triggerEventId)
        => triggerEventId != Guid.Empty && LastTriggerEventId == triggerEventId;

    /// <summary>Determines whether a trigger can create a workflow under the current reconstructed state.</summary>
    /// <param name="triggerEventId">Trigger event identity to inspect.</param>
    /// <returns>
    /// <see langword="true"/> when no workflow is running and the trigger is not the latest persisted request.
    /// </returns>
    public bool CanAcceptStart(Guid triggerEventId)
        => triggerEventId != Guid.Empty && !HasActiveWorkflow && !IsDuplicateTrigger(triggerEventId);

    /// <summary>Determines whether a pipeline result or failure event has already been accepted for any stage.</summary>
    /// <param name="sourceEventId">Pipeline event identity to inspect.</param>
    /// <returns><see langword="true"/> when the bounded stage metadata contains the identity.</returns>
    public bool HasProcessedPipelineEvent(Guid sourceEventId)
        => sourceEventId != Guid.Empty && _processedPipelineEventIds.Values.Contains(sourceEventId);

    /// <summary>Determines whether a previously accepted source event now carries different result content.</summary>
    /// <param name="sourceEventId">Pipeline event identity to inspect.</param>
    /// <param name="stage">Pipeline stage reported by the duplicate delivery.</param>
    /// <param name="result">Result envelope reported by the duplicate delivery.</param>
    /// <returns><see langword="true"/> when the source identity was accepted with different stage or result data.</returns>
    public bool IsConflictingPipelineResult(
        Guid sourceEventId,
        StrategyWorkflowStage stage,
        StrategyStageResultEnvelope result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (sourceEventId == Guid.Empty)
            return false;

        var accepted = _processedPipelineResults.Values
            .FirstOrDefault(value => value.SourceEventId == sourceEventId);
        return accepted is not null &&
               (accepted.Stage != stage ||
                accepted.ResultId != result.ResultId ||
                !string.Equals(accepted.PayloadSha256, result.PayloadSha256, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Determines whether a timeout operation has already been applied.</summary>
    /// <param name="timeoutId">Timeout operation identity to inspect.</param>
    /// <returns><see langword="true"/> when the bounded stage metadata contains the identity.</returns>
    public bool HasProcessedTimeout(Guid timeoutId)
        => timeoutId != Guid.Empty && _processedTimeoutIds.Values.Contains(timeoutId);

    /// <summary>Gets a deep copy of the original ITI trigger retained for the active workflow.</summary>
    internal FuturesItiSignalGeneratedEvent? ActiveTriggerEvent
        => _activeTriggerEvent is null ? null : CloneTrigger(_activeTriggerEvent);

    /// <summary>Gets the committed pipeline instruction available for deterministic live or recovery dispatch.</summary>
    internal IntrinsicTimeStrategyWorkflowDispatchInstruction? ActiveDispatchInstruction { get; private set; }

    /// <summary>Seeds only the public projection snapshot before applying a later committed event.</summary>
    /// <remarks>
    /// This is used exclusively by the conventional projector when it resumes from the rebuildable Scylla snapshot.
    /// It does not restore private command metadata and must never be used for authoritative Command-state recovery.
    /// </remarks>
    internal void RestoreProjectionSnapshot(
        IntrinsicTimeStrategyWorkflowState workflow,
        long lastPersistedEventId)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        _latestWorkflow = CloneWorkflow(workflow);
        EntityId = workflow.EntityId;
        LastPersistedEventId = lastPersistedEventId;
    }

    /// <summary>Applies one supported workflow event to the immutable workflow graph.</summary>
    /// <param name="domainEvent">Workflow event being applied or replayed.</param>
    /// <returns><see langword="true"/> when the event is supported and was applied.</returns>
    protected override bool Apply(IEvent domainEvent)
    {
        var applied = domainEvent switch
        {
            WorkflowStrategyStateUpdatedEvent e => On(e),
            _ => false
        };

        if (!applied)
            return false;

        AppliedEntityEventCount++;
        if (domainEvent is IEvent<IntrinsicTimeStrategyWorkflowEntityId> workflowEvent)
        {
            EntityId = workflowEvent.EntityId;
            LastPersistedEventId = Math.Max(LastPersistedEventId, workflowEvent.EventId);
        }

        return true;
    }

    /// <summary>Records the expected PostgreSQL stream version used by the next optimistic write.</summary>
    internal void SetPersistedStreamVersion(long value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        PersistedStreamVersion = value;
    }

    bool On(WorkflowStrategyStateUpdatedEvent e)
    {
        if (e.State.EntityId != e.EntityId || e.State.WorkflowId != e.WorkflowId ||
            e.State.WorkflowRevision != e.WorkflowRevision)
            return false;

        _currentView = CloneView(e.State);
        _latestWorkflow = ToLegacyWorkflow(e.State);
        _activeTriggerEvent = e.State.Status == WorkflowStrategyMachineStatus.Started
            ? CloneTrigger(e.State.TriggerEvent)
            : null;
        ActiveDispatchInstruction = null;
        LastTriggerEventId = e.State.TriggerEventId;
        LastRequestedWorkflowId = e.State.WorkflowId;
        LastStartDecision = e.State.Status == WorkflowStrategyMachineStatus.Started
            ? StrategyWorkflowStartDecision.Accepted
            : LastStartDecision;
        return true;
    }

    bool On(StrategyWorkflowStartAcceptedEvent e)
    {
        var firstStage = new StrategyWorkflowStageState
        {
            ProcessingStatus = StrategyActorProcessingStatus.Processing,
            StartedAtUtc = e.StartedAtUtc
        };

        _latestWorkflow = WithStage(
            new IntrinsicTimeStrategyWorkflowState
            {
                EntityId = e.EntityId,
                WorkflowId = e.WorkflowId,
                TriggerEventId = e.TriggerEventId,
                CorrelationId = e.CorrelationId,
                WorkflowDefinitionVersion = e.WorkflowDefinitionVersion,
                Status = StrategyWorkflowStatus.Running,
                Outcome = StrategyWorkflowOutcome.None,
                CurrentStage = e.Stage,
                WorkflowRevision = e.WorkflowRevision,
                StartedAtUtc = e.StartedAtUtc,
                RegimeDiscoveryParameterSet = e.RegimeDiscoveryParameterSet,
                RegimeDiscoveryParameterPayloadSha256 = e.RegimeDiscoveryParameterPayloadSha256
            },
            e.Stage,
            firstStage);

        _activeTriggerEvent = CloneTrigger(e.TriggerEvent);
        ActiveDispatchInstruction = null;
        TotalStartRequests++;
        AcceptedStartRequests++;
        SetLastStartDecision(
            e.CommandId,
            e.TriggerEventId,
            e.WorkflowId,
            StrategyWorkflowStartDecision.Accepted,
            e.StartedAtUtc);
        return true;
    }

    bool On(StrategyWorkflowStartRejectedEvent e)
    {
        TotalStartRequests++;
        RejectedStartRequests++;
        SetLastStartDecision(
            e.CommandId,
            e.TriggerEventId,
            e.RequestedWorkflowId,
            StrategyWorkflowStartDecision.Rejected,
            e.RejectedAtUtc);
        return true;
    }

    bool On(IntrinsicTimeStrategyWorkflowStartedEvent e)
    {
        var workflow = NormalizeDispatchWorkflow(
            e.WorkflowState,
            e.EntityId,
            e.WorkflowId,
            e.WorkflowRevision,
            e.CorrelationId,
            e.NextPipelineStage,
            e.StartedAtUtc);
        _latestWorkflow = workflow;
        _activeTriggerEvent = CloneTrigger(e.TriggerEvent);
        ActiveDispatchInstruction = new IntrinsicTimeStrategyWorkflowDispatchInstruction(
            e.NextPipelineStage,
            e.NextPipelineActorType,
            e.NextPipelineActorName,
            e.NextPipelineBoundedContext,
            e.NextPipelineCommandId,
            CloneWorkflow(workflow),
            CloneTrigger(e.TriggerEvent),
            e.RequestedAtUtc,
            e.ExpectedCompletionAtUtc);
        return true;
    }

    bool On(IntrinsicTimeStrategyWorkflowContinuedEvent e)
    {
        var workflow = NormalizeDispatchWorkflow(
            e.WorkflowState,
            e.EntityId,
            e.WorkflowId,
            e.WorkflowRevision,
            e.CorrelationId,
            e.NextPipelineStage,
            e.ContinuedAtUtc);
        _latestWorkflow = workflow;
        _activeTriggerEvent = CloneTrigger(e.TriggerEvent);
        ActiveDispatchInstruction = new IntrinsicTimeStrategyWorkflowDispatchInstruction(
            e.NextPipelineStage,
            e.NextPipelineActorType,
            e.NextPipelineActorName,
            e.NextPipelineBoundedContext,
            e.NextPipelineCommandId,
            CloneWorkflow(workflow),
            CloneTrigger(e.TriggerEvent),
            e.RequestedAtUtc,
            e.ExpectedCompletionAtUtc);
        return true;
    }

    bool OnResult(
        StrategyWorkflowStage stage,
        StrategyWorkflowId workflowId,
        long workflowRevision,
        Guid sourceEventId,
        StrategyStageResultEnvelope result,
        DateTime recordedAtUtc)
    {
        if (!TryGetWorkflow(workflowId, out var workflow))
            return false;

        var stageState = GetStage(workflow, stage) with
        {
            ProcessingStatus = StrategyActorProcessingStatus.Completed,
            CompletedAtUtc = recordedAtUtc,
            FailedAtUtc = null,
            Result = CloneResult(result),
            Failure = null
        };
        _latestWorkflow = WithStage(workflow with
        {
            CurrentStage = stage,
            WorkflowRevision = workflowRevision
        }, stage, stageState);
        if (sourceEventId != Guid.Empty)
        {
            _processedPipelineEventIds = _processedPipelineEventIds.SetItem(stage, sourceEventId);
            _processedPipelineResults = _processedPipelineResults.SetItem(stage,
                new StrategyPipelineResultIdentity(stage, sourceEventId, result.ResultId, result.PayloadSha256));
        }
        return true;
    }

    bool OnContinuation(
        StrategyWorkflowStage stage,
        StrategyWorkflowId workflowId,
        long workflowRevision,
        StrategyWorkflowContinuationDecision decision,
        string ruleSetId,
        int ruleSetVersion,
        string[] reasonCodes)
    {
        if (!TryGetWorkflow(workflowId, out var workflow))
            return false;

        var stageState = GetStage(workflow, stage) with
        {
            ContinuationDecision = decision,
            ContinuationRuleSetId = ruleSetId ?? string.Empty,
            ContinuationRuleSetVersion = ruleSetVersion,
            ContinuationReasonCodes = reasonCodes ?? []
        };
        _latestWorkflow = WithStage(workflow with
        {
            CurrentStage = stage,
            WorkflowRevision = workflowRevision
        }, stage, stageState);
        return true;
    }

    bool OnFailure(
        StrategyWorkflowStage stage,
        StrategyWorkflowId workflowId,
        long workflowRevision,
        Guid sourceEventId,
        StrategyPipelineFailure failure,
        DateTime failedAtUtc)
    {
        if (!TryGetWorkflow(workflowId, out var workflow))
            return false;

        var stageState = GetStage(workflow, stage) with
        {
            ProcessingStatus = StrategyActorProcessingStatus.Failed,
            FailedAtUtc = failedAtUtc,
            Failure = failure with { }
        };
        _latestWorkflow = WithStage(workflow with
        {
            CurrentStage = stage,
            WorkflowRevision = workflowRevision
        }, stage, stageState);
        if (sourceEventId != Guid.Empty)
            _processedPipelineEventIds = _processedPipelineEventIds.SetItem(stage, sourceEventId);
        return true;
    }

    bool OnTimeout(
        StrategyWorkflowStage stage,
        StrategyWorkflowId workflowId,
        long workflowRevision,
        Guid timeoutId,
        DateTime timedOutAtUtc)
    {
        if (!TryGetWorkflow(workflowId, out var workflow))
            return false;

        var stageState = GetStage(workflow, stage) with
        {
            ProcessingStatus = StrategyActorProcessingStatus.TimedOut,
            FailedAtUtc = timedOutAtUtc
        };
        _latestWorkflow = WithStage(workflow with
        {
            CurrentStage = stage,
            WorkflowRevision = workflowRevision
        }, stage, stageState);
        if (timeoutId != Guid.Empty)
            _processedTimeoutIds = _processedTimeoutIds.SetItem(stage, timeoutId);
        return true;
    }

    bool On(IntrinsicTimeStrategyWorkflowCompletedEvent e)
    {
        if (!TryGetWorkflow(e.WorkflowId, out var workflow))
            return false;

        _latestWorkflow = workflow with
        {
            Status = StrategyWorkflowStatus.Completed,
            Outcome = StrategyWorkflowOutcome.Completed,
            CurrentStage = e.Stage,
            WorkflowRevision = e.WorkflowRevision,
            TerminalAtUtc = e.CompletedAtUtc,
            StopReasonCode = string.Empty
        };
        ClearActiveExecutionMetadata();
        return true;
    }

    bool On(IntrinsicTimeStrategyWorkflowStoppedEvent e)
    {
        if (!TryGetWorkflow(e.WorkflowId, out var workflow))
            return false;

        var currentStage = GetStage(workflow, e.Stage);
        currentStage = e.Outcome switch
        {
            StrategyWorkflowOutcome.Cancelled => currentStage with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Cancelled,
                FailedAtUtc = e.StoppedAtUtc
            },
            StrategyWorkflowOutcome.TimedOut
                when currentStage.ProcessingStatus != StrategyActorProcessingStatus.TimedOut => currentStage with
                {
                    ProcessingStatus = StrategyActorProcessingStatus.TimedOut,
                    FailedAtUtc = e.StoppedAtUtc
                },
            _ => currentStage
        };

        _latestWorkflow = WithStage(workflow with
        {
            Status = StrategyWorkflowStatus.Stopped,
            Outcome = e.Outcome,
            CurrentStage = e.Stage,
            WorkflowRevision = e.WorkflowRevision,
            TerminalAtUtc = e.StoppedAtUtc,
            StopReasonCode = e.ReasonCode ?? string.Empty
        }, e.Stage, currentStage);
        ClearActiveExecutionMetadata();
        return true;
    }

    void SetLastStartDecision(
        Guid commandId,
        Guid triggerEventId,
        StrategyWorkflowId requestedWorkflowId,
        StrategyWorkflowStartDecision decision,
        DateTime requestedAtUtc)
    {
        LastStartCommandId = commandId;
        LastTriggerEventId = triggerEventId;
        LastRequestedWorkflowId = requestedWorkflowId;
        LastStartDecision = decision;
        LastStartRequestedAtUtc = requestedAtUtc;
    }

    void ClearActiveExecutionMetadata()
    {
        _activeTriggerEvent = null;
        ActiveDispatchInstruction = null;
    }

    bool TryGetWorkflow(
        StrategyWorkflowId workflowId,
        out IntrinsicTimeStrategyWorkflowState workflow)
    {
        if (_latestWorkflow is null || _latestWorkflow.WorkflowId != workflowId)
        {
            workflow = default!;
            return false;
        }

        workflow = _latestWorkflow;
        return true;
    }

    static IntrinsicTimeStrategyWorkflowState NormalizeDispatchWorkflow(
        IntrinsicTimeStrategyWorkflowState source,
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        StrategyWorkflowId workflowId,
        long workflowRevision,
        Guid correlationId,
        StrategyWorkflowStage nextStage,
        DateTime stageStartedAtUtc)
    {
        var workflow = CloneWorkflow(source) with
        {
            EntityId = entityId,
            WorkflowId = workflowId,
            CorrelationId = correlationId,
            Status = StrategyWorkflowStatus.Running,
            Outcome = StrategyWorkflowOutcome.None,
            CurrentStage = nextStage,
            WorkflowRevision = workflowRevision,
            TerminalAtUtc = null,
            StopReasonCode = string.Empty
        };
        var stageState = GetStage(workflow, nextStage) with
        {
            ProcessingStatus = StrategyActorProcessingStatus.Processing,
            StartedAtUtc = GetStage(workflow, nextStage).StartedAtUtc ?? stageStartedAtUtc,
            CompletedAtUtc = null,
            FailedAtUtc = null,
            Failure = null
        };
        return WithStage(workflow, nextStage, stageState);
    }

    static StrategyWorkflowStageState GetStage(
        IntrinsicTimeStrategyWorkflowState workflow,
        StrategyWorkflowStage stage)
        => stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => workflow.RegimeDiscovery,
            StrategyWorkflowStage.MarketCondition => workflow.MarketCondition,
            StrategyWorkflowStage.TradeSelection => workflow.TradeSelection,
            StrategyWorkflowStage.OrderComposition => workflow.OrderComposition,
            StrategyWorkflowStage.RiskManagement => workflow.RiskManagement,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "A concrete workflow stage is required.")
        };

    static IntrinsicTimeStrategyWorkflowState WithStage(
        IntrinsicTimeStrategyWorkflowState workflow,
        StrategyWorkflowStage stage,
        StrategyWorkflowStageState stageState)
        => stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => workflow with { RegimeDiscovery = CloneStage(stageState) },
            StrategyWorkflowStage.MarketCondition => workflow with { MarketCondition = CloneStage(stageState) },
            StrategyWorkflowStage.TradeSelection => workflow with { TradeSelection = CloneStage(stageState) },
            StrategyWorkflowStage.OrderComposition => workflow with { OrderComposition = CloneStage(stageState) },
            StrategyWorkflowStage.RiskManagement => workflow with { RiskManagement = CloneStage(stageState) },
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "A concrete workflow stage is required.")
        };

    static IntrinsicTimeStrategyWorkflowState CloneWorkflow(IntrinsicTimeStrategyWorkflowState source)
        => source with
        {
            RegimeDiscovery = CloneStage(source.RegimeDiscovery),
            MarketCondition = CloneStage(source.MarketCondition),
            TradeSelection = CloneStage(source.TradeSelection),
            OrderComposition = CloneStage(source.OrderComposition),
            RiskManagement = CloneStage(source.RiskManagement)
        };

    static IntrinsicTimeStrategyWorkflowView CloneView(IntrinsicTimeStrategyWorkflowView source)
        => source with
        {
            RegimeDiscovery = CloneStage(source.RegimeDiscovery),
            MarketCondition = CloneStage(source.MarketCondition),
            TradeSelection = CloneStage(source.TradeSelection),
            OrderComposition = CloneStage(source.OrderComposition),
            RiskManagement = CloneStage(source.RiskManagement),
            TriggerEvent = CloneTrigger(source.TriggerEvent),
            RegimeDiscoveryParameterSet = CloneParameterSet(source.RegimeDiscoveryParameterSet),
            MarketConditionParameterSet = CloneMarketConditionParameterSet(source.MarketConditionParameterSet)
        };

    static IntrinsicTimeStrategyWorkflowState ToLegacyWorkflow(IntrinsicTimeStrategyWorkflowView source)
        => new()
        {
            EntityId = source.EntityId,
            WorkflowId = source.WorkflowId,
            TriggerEventId = source.TriggerEventId,
            CorrelationId = source.CorrelationId,
            WorkflowDefinitionVersion = source.WorkflowDefinitionVersion,
            Status = source.Status switch
            {
                WorkflowStrategyMachineStatus.Empty => StrategyWorkflowStatus.None,
                WorkflowStrategyMachineStatus.Started => StrategyWorkflowStatus.Running,
                WorkflowStrategyMachineStatus.Completed => StrategyWorkflowStatus.Completed,
                _ => StrategyWorkflowStatus.Stopped
            },
            Outcome = source.Outcome != StrategyWorkflowOutcome.None ? source.Outcome : source.Status switch
            {
                WorkflowStrategyMachineStatus.Completed => StrategyWorkflowOutcome.Completed,
                WorkflowStrategyMachineStatus.Failed => StrategyWorkflowOutcome.PipelineFailed,
                WorkflowStrategyMachineStatus.TimedOut => StrategyWorkflowOutcome.TimedOut,
                WorkflowStrategyMachineStatus.Cancelled => StrategyWorkflowOutcome.Cancelled,
                _ => StrategyWorkflowOutcome.None
            },
            CurrentStage = source.CurrentStage,
            WorkflowRevision = source.WorkflowRevision,
            StartedAtUtc = source.StartedAtUtc,
            TerminalAtUtc = source.TerminalAtUtc,
            RegimeDiscovery = CloneStage(source.RegimeDiscovery),
            MarketCondition = CloneStage(source.MarketCondition),
            TradeSelection = CloneStage(source.TradeSelection),
            OrderComposition = CloneStage(source.OrderComposition),
            RiskManagement = CloneStage(source.RiskManagement),
            StopReasonCode = source.StopReasonCode,
            RegimeDiscoveryParameterSet = CloneParameterSet(source.RegimeDiscoveryParameterSet),
            RegimeDiscoveryParameterPayloadSha256 = source.RegimeDiscoveryParameterPayloadSha256,
            FundId = source.FundId,
            MarketConditionParameterSet = CloneMarketConditionParameterSet(source.MarketConditionParameterSet),
            MarketConditionParameterPayloadSha256 = source.MarketConditionParameterPayloadSha256
        };

    static StrategyWorkflowStageState CloneStage(StrategyWorkflowStageState source)
        => source with
        {
            Result = source.Result is null ? null : CloneResult(source.Result),
            ContinuationReasonCodes = source.ContinuationReasonCodes,
            Failure = source.Failure is null ? null : source.Failure with { }
        };

    static StrategyStageResultEnvelope CloneResult(StrategyStageResultEnvelope source)
        => source with { Payload = source.Payload };

    static Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSet
        CloneParameterSet(
            Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSet source)
        => MessagePackSerializer.Deserialize<
            Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSet>(
            MessagePackSerializer.Serialize(source));

    static Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition.MarketConditionParameterSet
        CloneMarketConditionParameterSet(
            Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition.MarketConditionParameterSet source)
        => MessagePackSerializer.Deserialize<
            Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition.MarketConditionParameterSet>(
            MessagePackSerializer.Serialize(source));

    static FuturesItiSignalGeneratedEvent CloneTrigger(FuturesItiSignalGeneratedEvent source)
        => source with
        {
            FuturesItiSignal = source.FuturesItiSignal is null ? null : source.FuturesItiSignal with { }
        };
}

/// <summary>
/// Represents the bounded committed instruction required to dispatch or redispatch the current strategy pipeline.
/// </summary>
/// <param name="Stage">Pipeline stage selected by the Workflow Command actor.</param>
/// <param name="ActorType">Target pipeline actor type.</param>
/// <param name="ActorName">Target pipeline actor name.</param>
/// <param name="BoundedContext">Target pipeline bounded context.</param>
/// <param name="CommandId">Deterministic pipeline command identity.</param>
/// <param name="WorkflowState">Immutable workflow input snapshot.</param>
/// <param name="TriggerEvent">Original ITI trigger retained for pipeline input.</param>
/// <param name="RequestedAtUtc">UTC pipeline request timestamp.</param>
/// <param name="ExpectedCompletionAtUtc">Optional UTC stage deadline.</param>
internal sealed record IntrinsicTimeStrategyWorkflowDispatchInstruction(
    StrategyWorkflowStage Stage,
    ActorType ActorType,
    string ActorName,
    BoundedContextName BoundedContext,
    Guid CommandId,
    IntrinsicTimeStrategyWorkflowState WorkflowState,
    FuturesItiSignalGeneratedEvent TriggerEvent,
    DateTime RequestedAtUtc,
    DateTime? ExpectedCompletionAtUtc);

/// <summary>Retains bounded result identity metadata needed to distinguish duplicate and conflicting deliveries.</summary>
internal sealed record StrategyPipelineResultIdentity(
    StrategyWorkflowStage Stage,
    Guid SourceEventId,
    Guid ResultId,
    string PayloadSha256);
