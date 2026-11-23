using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Snapshots;
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
/// graph. Runtime recovery restores the latest checkpoint and replays the remaining workflow snapshots and queue admissions.
/// </remarks>
public sealed class IntrinsicTimeStrategyWorkflowCommandState : BaseEventSourceActorState<IntrinsicTimeStrategyWorkflowCommandState>, IEventSourceActorState<IntrinsicTimeStrategyWorkflowCommandState>
{
    /// <summary>Applies an already validated ordered workflow snapshot batch.</summary>
    /// <param name = "workflowEvents">The command-owned source events.</param>
    /// <param name = "command">The originating workflow command.</param>
    /// <returns>True when all snapshots apply, including an idempotent empty batch.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3267", Justification = "Apply aggregate events in sequence and stop on the first rejected transition; this loop mutates state.")]
    internal bool Update(IReadOnlyList<WorkflowStrategyStateUpdatedEvent> workflowEvents, ICommand command)
    {
        if (workflowEvents.Any(sourceEvent => sourceEvent.CommandId != command.CommandId))
            return false;
        foreach (var sourceEvent in workflowEvents)
            if (!Update(sourceEvent, command))
                return false;
        return true;
    }

    IntrinsicTimeStrategyWorkflowView? _workflowDefinition;
    IntrinsicTimeStrategyWorkflowState? _latestWorkflow;
    FuturesItiSignalGeneratedEvent? _activeTriggerEvent;
    readonly Queue<ExecuteIntrinsicTimeStrategyWorkflowCommand> _pendingSignals = new();
    readonly HashSet<Guid> _knownTriggerIds = [];
    internal ExecuteIntrinsicTimeStrategyWorkflowCommand? NextSignal => _pendingSignals.TryPeek(out var next) ? next : null;
    internal bool QueueChanged { get; private set; }
    internal void RememberStartedTrigger(Guid triggerId) => _knownTriggerIds.Add(triggerId);
    internal WorkflowStrategyStateUpdatedEvent? LatestSource { get; private set; }
    readonly ImmutableDictionary<StrategyWorkflowStage, Guid> _processedPipelineEventIds = ImmutableDictionary<StrategyWorkflowStage, Guid>.Empty;
    readonly ImmutableDictionary<StrategyWorkflowStage, StrategyPipelineResultIdentity> _processedPipelineResults = ImmutableDictionary<StrategyWorkflowStage, StrategyPipelineResultIdentity>.Empty;
    readonly ImmutableDictionary<StrategyWorkflowStage, Guid> _processedTimeoutIds = ImmutableDictionary<StrategyWorkflowStage, Guid>.Empty;
    /// <summary>Gets or sets the actor-thread identity assigned by the actor framework.</summary>
    public override ActorThreadId Id { get; set; } = default!;
    /// <summary>Gets the workflow entity associated with the replayed stream.</summary>
    public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; private set; } = new();
    /// <summary>Gets whether an authoritative state-update snapshot has been applied.</summary>
    public bool HasAuthoritativeSnapshot => _workflowDefinition is not null;
    /// <summary>Gets a defensive copy of the latest authoritative workflow view.</summary>
    public IntrinsicTimeStrategyWorkflowView? CurrentView => WorkflowDefinition;
    /// <summary>Gets a defensive copy of the authoritative workflow definition.</summary>
    public IntrinsicTimeStrategyWorkflowView? WorkflowDefinition => _workflowDefinition is null ? null : CloneView(_workflowDefinition);
    /// <summary>Manager-owned immutable input references; historical transport keeps its defensive-copy boundary.</summary>
    internal IntrinsicTimeStrategyWorkflowView? DecisionView => _workflowDefinition is { DecisionRuntimeVersion: > 0 }
        ? _workflowDefinition : WorkflowDefinition;
    /// <summary>Gets the PostgreSQL stream version observed when this state was loaded.</summary>
    public long PersistedStreamVersion { get; private set; }
    /// <summary>Gets a value indicating whether this entity currently has a running workflow.</summary>
    public bool HasActiveWorkflow => _latestWorkflow is { Status: StrategyWorkflowStatus.Running };
    /// <summary>Gets a deep immutable copy of the active workflow, or <see langword="null"/> when terminal or empty.</summary>
    public IntrinsicTimeStrategyWorkflowState? ActiveWorkflow => HasActiveWorkflow ? CloneWorkflow(_latestWorkflow!) : null;
    /// <summary>Gets a deep immutable copy of the most recently reconstructed workflow, including terminal state.</summary>
    public IntrinsicTimeStrategyWorkflowState? LatestWorkflow => _latestWorkflow is null ? null : CloneWorkflow(_latestWorkflow);
    /// <summary>Gets the total number of persisted start decisions applied to this state.</summary>
    public long TotalStartRequests { get; }
    /// <summary>Gets the number of accepted start decisions applied to this state.</summary>
    public long AcceptedStartRequests { get; }
    /// <summary>Gets the number of rejected start decisions applied to this state.</summary>
    public long RejectedStartRequests { get; }
    /// <summary>Gets the command identity associated with the latest start decision.</summary>
    public Guid? LastStartCommandId { get; }
    /// <summary>Gets the trigger-event identity associated with the latest start decision.</summary>
    public Guid? LastTriggerEventId { get; private set; }
    /// <summary>Gets the workflow identity proposed by the latest start decision.</summary>
    public StrategyWorkflowId? LastRequestedWorkflowId { get; private set; }
    /// <summary>Gets the latest persisted start decision.</summary>
    public StrategyWorkflowStartDecision LastStartDecision { get; private set; }
    /// <summary>Gets the timestamp associated with the latest start decision.</summary>
    public DateTime? LastStartRequestedAtUtc { get; }
    /// <summary>Gets the number of supported workflow events applied to this state instance.</summary>
    public long AppliedEntityEventCount { get; private set; }
    /// <summary>Gets the greatest persisted event-stream identity observed during application or replay.</summary>
    public long LastPersistedEventId { get; private set; }

    /// <summary>Determines whether the supplied trigger is already queued or has started a workflow.</summary>
    /// <param name = "triggerEventId">Trigger event identity to inspect.</param>
    /// <returns><see langword="true"/> when the trigger is known to the aggregate.</returns>
    public bool IsDuplicateTrigger(Guid triggerEventId) => _knownTriggerIds.Contains(triggerEventId);
    /// <summary>Determines whether a trigger can create a workflow under the current reconstructed state.</summary>
    /// <param name = "triggerEventId">Trigger event identity to inspect.</param>
    /// <returns>
    /// <see langword="true"/> when no workflow is running and the trigger is not the latest persisted request.
    /// </returns>
    public bool CanAcceptStart(Guid triggerEventId) => triggerEventId != Guid.Empty && !HasActiveWorkflow &&
        NextSignal is null && !IsDuplicateTrigger(triggerEventId);
    /// <summary>Determines whether a pipeline result or failure event has already been accepted for any stage.</summary>
    /// <param name = "sourceEventId">Pipeline event identity to inspect.</param>
    /// <returns><see langword="true"/> when the bounded stage metadata contains the identity.</returns>
    public bool HasProcessedPipelineEvent(Guid sourceEventId) => sourceEventId != Guid.Empty && _processedPipelineEventIds.ContainsValue(sourceEventId);
    /// <summary>Determines whether a previously accepted source event now carries different result content.</summary>
    /// <param name = "sourceEventId">Pipeline event identity to inspect.</param>
    /// <param name = "stage">Pipeline stage reported by the duplicate delivery.</param>
    /// <param name = "result">Result envelope reported by the duplicate delivery.</param>
    /// <returns><see langword="true"/> when the source identity was accepted with different stage or result data.</returns>
    public bool IsConflictingPipelineResult(Guid sourceEventId, StrategyWorkflowStage stage, StrategyStageResultEnvelope result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (sourceEventId == Guid.Empty)
            return false;
        var accepted = _processedPipelineResults.Values.FirstOrDefault(value => value.SourceEventId == sourceEventId);
        return accepted is not null && (accepted.Stage != stage || accepted.ResultId != result.ResultId || !string.Equals(accepted.PayloadSha256, result.PayloadSha256, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Determines whether a timeout operation has already been applied.</summary>
    /// <param name = "timeoutId">Timeout operation identity to inspect.</param>
    /// <returns><see langword="true"/> when the bounded stage metadata contains the identity.</returns>
    public bool HasProcessedTimeout(Guid timeoutId) => timeoutId != Guid.Empty && _processedTimeoutIds.ContainsValue(timeoutId);
    /// <summary>Gets a deep copy of the original ITI trigger retained for the active workflow.</summary>
    internal FuturesItiSignalGeneratedEvent? ActiveTriggerEvent => _activeTriggerEvent is null ? null : CloneTrigger(_activeTriggerEvent);
    /// <summary>Gets the committed pipeline instruction available for deterministic live or recovery dispatch.</summary>
    internal IntrinsicTimeStrategyWorkflowDispatchInstruction? ActiveDispatchInstruction { get; private set; }

    /// <summary>Seeds only the public projection snapshot before applying a later committed event.</summary>
    /// <remarks>
    /// This is used exclusively by the conventional projector when it resumes from the rebuildable Scylla snapshot.
    /// It does not restore private command metadata and must never be used for authoritative Command-state recovery.
    /// </remarks>
    internal void RestoreProjectionSnapshot(IntrinsicTimeStrategyWorkflowState workflow, long lastPersistedEventId)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        _latestWorkflow = CloneWorkflow(workflow);
        EntityId = workflow.EntityId;
        LastPersistedEventId = lastPersistedEventId;
    }

    /// <summary>Applies one supported workflow event to the immutable workflow graph.</summary>
    /// <param name = "domainEvent">Workflow event being applied or replayed.</param>
    /// <returns><see langword="true"/> when the event is supported and was applied.</returns>
    internal IntrinsicTimeStrategyWorkflowStateSnapshottedEvent CaptureSnapshot(
        ICommand command, WorkflowStrategyStateUpdatedEvent latest) => new()
    {
        Subject = new(ActorType.Event, "IntrinsicTimeStrategyWorkflow", "StateSnapshotted", command.Subject.EntityId),
        Id = Guid.NewGuid(),
        CommandId = command.CommandId,
        EventSource = command.EventSource,

        Latest = latest,
        AppliedEventCount = AppliedEntityEventCount,
        LastStartDecision = LastStartDecision,
        PendingSignals = [.. _pendingSignals]
    };

    internal bool RestoreSnapshot(IntrinsicTimeStrategyWorkflowStateSnapshottedEvent snapshot)
    {
        if (snapshot.Latest is not { EventId: > 0 } latest || snapshot.AppliedEventCount <= 0 ||
            HasAuthoritativeSnapshot && latest.EntityId != EntityId || !Apply(latest, addEvent: false))
            return false;
        AppliedEntityEventCount = snapshot.AppliedEventCount;
        LastStartDecision = snapshot.LastStartDecision;
        if (!snapshot.PendingSignals.IsDefault)
            foreach (var request in snapshot.PendingSignals)
            {
                if (request.EntityId != EntityId || request.TriggerEventId == Guid.Empty || !_knownTriggerIds.Add(request.TriggerEventId))
                    return false;
                _pendingSignals.Enqueue(request);
            }
        return true;
    }

    protected override bool Apply(IEvent domainEvent)
    {
        var applied = domainEvent switch
        {
            WorkflowStrategyStateUpdatedEvent e => On(e),
            IntrinsicTimeStrategySignalQueuedEvent e => OnQueued(e),
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
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        PersistedStreamVersion = value;
    }

    bool On(WorkflowStrategyStateUpdatedEvent e)
    {
        if (e.State.EntityId != e.EntityId || e.State.WorkflowId != e.WorkflowId || e.State.WorkflowRevision != e.WorkflowRevision)
            return false;
        if (e.State.TriggerEventId != Guid.Empty)
            _knownTriggerIds.Add(e.State.TriggerEventId);
        if (e.State.Status == WorkflowStrategyMachineStatus.Started && e.WorkflowRevision == 1 &&
            NextSignal?.TriggerEventId == e.State.TriggerEventId)
        {
            _pendingSignals.Dequeue();
            QueueChanged = true;
        }
        LatestSource = e;
        using (WorkflowTrace.Start("workflow.state.clone_view", e.State))
            _workflowDefinition = CloneView(e.State);
        using (WorkflowTrace.Start("workflow.state.legacy_view", e.State))
            _latestWorkflow = ToLegacyWorkflow(e.State);
        _activeTriggerEvent = e.State.Status == WorkflowStrategyMachineStatus.Started ? CloneTrigger(e.State.TriggerEvent) : null;
        ActiveDispatchInstruction = null;
        LastTriggerEventId = e.State.TriggerEventId;
        LastRequestedWorkflowId = e.State.WorkflowId;
        LastStartDecision = e.State.Status == WorkflowStrategyMachineStatus.Started ? StrategyWorkflowStartDecision.Accepted : LastStartDecision;
        return true;
    }

    bool OnQueued(IntrinsicTimeStrategySignalQueuedEvent source)
    {
        var request = source.Request;
        if (!HasAuthoritativeSnapshot || source.EntityId != EntityId || request.EntityId != EntityId ||
            request.TriggerEventId == Guid.Empty || !_knownTriggerIds.Add(request.TriggerEventId))
            return false;
        _pendingSignals.Enqueue(request);
        QueueChanged = true;
        return true;
    }

    internal void ResetQueueChangeTracking() => QueueChanged = false;
    /// <summary>Forks committed owner state without serialization. Private graphs are replaced, never mutated in place.</summary>
    internal IntrinsicTimeStrategyWorkflowCommandState ForkCommitted()
    {
        if (Events.Count != 0) throw new InvalidOperationException("Uncommitted workflow state cannot be published.");
        var fork = new IntrinsicTimeStrategyWorkflowCommandState
        {
            Id = Id, EntityId = EntityId, _workflowDefinition = _workflowDefinition, _latestWorkflow = _latestWorkflow,
            _activeTriggerEvent = _activeTriggerEvent, LatestSource = LatestSource, PersistedStreamVersion = PersistedStreamVersion,
            AppliedEntityEventCount = AppliedEntityEventCount, LastPersistedEventId = LastPersistedEventId,
            LastTriggerEventId = LastTriggerEventId, LastRequestedWorkflowId = LastRequestedWorkflowId,
            LastStartDecision = LastStartDecision, ActiveDispatchInstruction = ActiveDispatchInstruction
        };
        fork._knownTriggerIds.UnionWith(_knownTriggerIds);
        foreach (var signal in _pendingSignals) fork._pendingSignals.Enqueue(signal);
        fork.RestoreCommittedStreamVersion(CommittedStreamVersion);
        return fork;
    }
    internal IntrinsicTimeStrategyWorkflowView? TrustedCommittedView => _workflowDefinition;
    static T? CopySelection<T>(T? value)
        where T : class => value is null ? null : MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(value));
    static IntrinsicTimeStrategyWorkflowState CloneWorkflow(IntrinsicTimeStrategyWorkflowState source) => source with
    {
        RegimeDiscovery = CloneStage(source.RegimeDiscovery),
        MarketCondition = CloneStage(source.MarketCondition),
        TradeSelection = CloneStage(source.TradeSelection),
        OrderComposition = CloneStage(source.OrderComposition),
        RiskManagement = CloneStage(source.RiskManagement),
        SelectionBinding = CopySelection(source.SelectionBinding),
        CompositionHandoff = CopySelection(source.CompositionHandoff),
        SelectionDispatch = CopySelection(source.SelectionDispatch),
        CompositionDispatch = CopySelection(source.CompositionDispatch)
    };
    static IntrinsicTimeStrategyWorkflowView CloneView(IntrinsicTimeStrategyWorkflowView source) => source with
    {
        RegimeDiscovery = CloneStage(source.RegimeDiscovery),
        MarketCondition = CloneStage(source.MarketCondition),
        TradeSelection = CloneStage(source.TradeSelection),
        OrderComposition = CloneStage(source.OrderComposition),
        RiskManagement = CloneStage(source.RiskManagement),
        TriggerEvent = CloneTrigger(source.TriggerEvent),
        RegimeDiscoveryParameterSet = CloneParameterSet(source.RegimeDiscoveryParameterSet),
        MarketConditionParameterSet = CloneMarketConditionParameterSet(source.MarketConditionParameterSet),
        AssessmentBinding = source.AssessmentBinding,
        SelectionBinding = CopySelection(source.SelectionBinding),
        CompositionHandoff = CopySelection(source.CompositionHandoff),
        SelectionDispatch = CopySelection(source.SelectionDispatch),
        CompositionDispatch = CopySelection(source.CompositionDispatch),
        PortfolioRiskDecision = CopySelection(source.PortfolioRiskDecision),
        EntryCommit = CopySelection(source.EntryCommit)
    };
    static IntrinsicTimeStrategyWorkflowState ToLegacyWorkflow(IntrinsicTimeStrategyWorkflowView source) => new()
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
        RegimeDiscoveryParameterApplication = source.RegimeDiscoveryParameterApplication,
        FundId = source.FundId,
        MarketConditionParameterSet = CloneMarketConditionParameterSet(source.MarketConditionParameterSet),
        MarketConditionParameterPayloadSha256 = source.MarketConditionParameterPayloadSha256,
        AssessmentBinding = source.AssessmentBinding,
        CompositionContracts = source.CompositionContracts,
        CompositionExecution = source.CompositionExecution,
        SelectionBinding = CopySelection(source.SelectionBinding),
        CompositionHandoff = CopySelection(source.CompositionHandoff),
        SelectionDispatch = CopySelection(source.SelectionDispatch),
        CompositionDispatch = CopySelection(source.CompositionDispatch)
    };
    static StrategyWorkflowStageState CloneStage(StrategyWorkflowStageState source) => source with
    {
        Result = source.Result is null ? null : CloneResult(source.Result),
        ContinuationReasonCodes = source.ContinuationReasonCodes,
        Failure = source.Failure is null ? null : source.Failure with
        {
        }
    };
    static StrategyStageResultEnvelope CloneResult(StrategyStageResultEnvelope source) => source with
    {
        Payload = source.Payload
    };
    static Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSet CloneParameterSet(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSet source) => MessagePackSerializer.Deserialize<Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery.RegimeDiscoveryParameterSet>(MessagePackSerializer.Serialize(source));
    static Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition.MarketConditionParameterSet CloneMarketConditionParameterSet(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition.MarketConditionParameterSet source) => MessagePackSerializer.Deserialize<Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition.MarketConditionParameterSet>(MessagePackSerializer.Serialize(source));
    static FuturesItiSignalGeneratedEvent CloneTrigger(FuturesItiSignalGeneratedEvent source) => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(source, source with
    {
        FuturesItiSignal = source.FuturesItiSignal is null ? null : source.FuturesItiSignal with
        {
        }
    });
}
