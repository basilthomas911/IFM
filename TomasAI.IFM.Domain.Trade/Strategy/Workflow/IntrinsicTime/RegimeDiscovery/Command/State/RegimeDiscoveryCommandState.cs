using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.State;

/// <summary>Identifies the private durable terminal state of Regime Discovery.</summary>
public enum RegimeDiscoveryCommandStatus : byte
{
    /// <summary>No accepted calculation has completed.</summary>
    Empty = 0,
    /// <summary>The calculation completed successfully.</summary>
    Completed = 1,
    /// <summary>The calculation failed durably.</summary>
    Failed = 2
}

/// <summary>Reconstructs the authoritative private Regime Discovery state from terminal domain events.</summary>
public sealed class RegimeDiscoveryCommandState
    : BaseEventSourceActorState<RegimeDiscoveryCommandState>,
      IEventSourceActorState<RegimeDiscoveryCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;
    /// <summary>Gets the workflow entity associated with this event stream.</summary>
    public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; private set; }
    /// <summary>Gets the owning workflow execution.</summary>
    public StrategyWorkflowId WorkflowId { get; private set; }
    /// <summary>Gets the immutable workflow input revision.</summary>
    public long InputWorkflowRevision { get; private set; }
    /// <summary>Gets the command that produced the terminal state.</summary>
    public Guid CommandId { get; private set; }
    /// <summary>Gets the exact frozen parameter payload hash.</summary>
    public string ParameterPayloadSha256 { get; private set; } = string.Empty;
    /// <summary>Gets the captured signal-snapshot identity.</summary>
    public Guid SignalSnapshotId { get; private set; }
    /// <summary>Gets the terminal state.</summary>
    public RegimeDiscoveryCommandStatus Status { get; private set; }
    /// <summary>Gets the complete typed result when successful.</summary>
    public RegimeDiscoveryResult? Result { get; private set; }
    /// <summary>Gets the standard durable failure when unsuccessful.</summary>
    public StrategyPipelineFailure? Failure { get; private set; }
    /// <summary>Gets stable failure reasons.</summary>
    public RegimeDiscoveryReason[] Reasons { get; private set; } = [];
    /// <summary>Gets the terminal result hash when successful.</summary>
    public string ResultPayloadSha256 { get; private set; } = string.Empty;
    /// <summary>Gets the latest persisted event sequence observed during replay.</summary>
    public long LastPersistedEventId { get; private set; }
    /// <summary>Gets whether the aggregate has a durable terminal event.</summary>
    public bool IsTerminal => Status is RegimeDiscoveryCommandStatus.Completed or RegimeDiscoveryCommandStatus.Failed;

    /// <summary>Determines whether an Execute command is an idempotent duplicate of the durable terminal input.</summary>
    /// <param name="command">Execute command to compare.</param>
    /// <returns><see langword="true"/> when workflow, revision, and parameter hash match.</returns>
    public bool Matches(ExecuteRegimeDiscoveryPipelineCommand command) => IsTerminal &&
        WorkflowId == command.WorkflowId &&
        InputWorkflowRevision == command.InputWorkflowRevision &&
        string.Equals(ParameterPayloadSha256, command.ParameterPayloadSha256, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        switch (domainEvent)
        {
            case RegimeDiscoveryCalculationCompletedEvent completed:
                EntityId = completed.EntityId;
                WorkflowId = completed.WorkflowId;
                InputWorkflowRevision = completed.InputWorkflowRevision;
                CommandId = completed.CommandId;
                ParameterPayloadSha256 = completed.ParameterPayloadSha256;
                SignalSnapshotId = completed.SignalSnapshotId;
                Result = completed.Result;
                ResultPayloadSha256 = completed.ResultPayloadSha256;
                Failure = null;
                Reasons = completed.Result.Reasons.ToArray();
                Status = RegimeDiscoveryCommandStatus.Completed;
                LastPersistedEventId = Math.Max(LastPersistedEventId, completed.EventId);
                return true;
            case RegimeDiscoveryCalculationFailedEvent failed:
                EntityId = failed.EntityId;
                WorkflowId = failed.WorkflowId;
                InputWorkflowRevision = failed.InputWorkflowRevision;
                CommandId = failed.CommandId;
                ParameterPayloadSha256 = failed.ParameterPayloadSha256;
                SignalSnapshotId = failed.SignalSnapshotId;
                Result = null;
                ResultPayloadSha256 = string.Empty;
                Failure = failed.Failure;
                Reasons = failed.Reasons.ToArray();
                Status = RegimeDiscoveryCommandStatus.Failed;
                LastPersistedEventId = Math.Max(LastPersistedEventId, failed.EventId);
                return true;
            default:
                return false;
        }
    }
}
