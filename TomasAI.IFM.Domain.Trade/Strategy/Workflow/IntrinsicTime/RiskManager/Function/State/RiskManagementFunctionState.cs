using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.State;

public sealed class RiskManagementFunctionState
    : BaseEventSourceActorState<RiskManagementFunctionState>,
      IEventSourceFunctionState<RiskManagementFunctionState, ExecuteRiskManagementPipelineCommand,
          RiskManagementFunctionCompletedEvent>
{
    public override ActorThreadId Id { get; set; } = default!;
    public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; private set; }
    public StrategyWorkflowId WorkflowId { get; private set; }
    public long InputWorkflowRevision { get; private set; }
    public Guid CommandId { get; private set; }
    public string ParameterPayloadSha256 { get; private set; } = string.Empty;
    public RiskManagementFunctionCompletedEvent? CompletedEvent { get; private set; }
    public bool IsCompleted => CompletedEvent is not null;
    public long LastPersistedEventId { get; private set; }

    public bool Matches(ExecuteRiskManagementPipelineCommand request) => IsCompleted &&
        CompletedEvent!.RequestFingerprint == request.Fingerprint();
    public bool TryComplete(RiskManagementFunctionCompletedEvent completed,
        ExecuteRiskManagementPipelineCommand request) => !IsCompleted && Update(completed, request);
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not RiskManagementFunctionCompletedEvent completed) return false;
        EntityId = completed.EntityId; WorkflowId = completed.WorkflowId;
        InputWorkflowRevision = completed.InputWorkflowRevision; CommandId = completed.CommandId;
        ParameterPayloadSha256 = completed.ParameterPayloadSha256; CompletedEvent = completed;
        LastPersistedEventId = Math.Max(LastPersistedEventId, completed.EventId); return true;
    }
}
