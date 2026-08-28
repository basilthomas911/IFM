using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.State;

/// <summary>Completed-only event-sourced state for one Regime Discovery Function execution.</summary>
public sealed class RegimeDiscoveryFunctionState
    : BaseEventSourceActorState<RegimeDiscoveryFunctionState>,
      IEventSourceFunctionState<
          RegimeDiscoveryFunctionState,
          ExecuteRegimeDiscoveryPipelineCommand,
          RegimeDiscoveryPipelineCompletedEvent>
{
    public override ActorThreadId Id { get; set; } = default!;
    public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; private set; }
    public StrategyWorkflowId WorkflowId { get; private set; }
    public long InputWorkflowRevision { get; private set; }
    public Guid CommandId { get; private set; }
    public string ParameterPayloadSha256 { get; private set; } = string.Empty;
    public RegimeDiscoveryPipelineCompletedEvent? CompletedEvent { get; private set; }
    public bool IsCompleted => CompletedEvent is not null;
    public long LastPersistedEventId { get; private set; }

    public bool Matches(ExecuteRegimeDiscoveryPipelineCommand request)
        => IsCompleted &&
           WorkflowId == request.WorkflowId &&
           InputWorkflowRevision == request.InputWorkflowRevision &&
           string.Equals(
               ParameterPayloadSha256,
               request.ParameterPayloadSha256,
               StringComparison.OrdinalIgnoreCase);

    public bool TryComplete(
        RegimeDiscoveryPipelineCompletedEvent completedEvent,
        ExecuteRegimeDiscoveryPipelineCommand request)
        => !IsCompleted && Update(completedEvent, request);

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not RegimeDiscoveryPipelineCompletedEvent completed)
            return false;

        EntityId = completed.EntityId;
        WorkflowId = completed.WorkflowId;
        InputWorkflowRevision = completed.InputWorkflowRevision;
        CommandId = completed.CommandId;
        ParameterPayloadSha256 = completed.ParameterPayloadSha256;
        CompletedEvent = completed;
        LastPersistedEventId = Math.Max(LastPersistedEventId, completed.EventId);
        return true;
    }
}
