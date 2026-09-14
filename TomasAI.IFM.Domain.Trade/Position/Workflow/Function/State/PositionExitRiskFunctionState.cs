using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Position.Workflow.Function.State;

public sealed class PositionExitRiskFunctionState : BaseEventSourceActorState<PositionExitRiskFunctionState>,
    IEventSourceFunctionState<PositionExitRiskFunctionState, EvaluatePositionExitRiskCommand,
        PositionExitRiskCompletedEvent>
{
    Guid activeCommandId;
    PositionExitRiskCompletedEvent? latest;
    public override ActorThreadId Id { get; set; } = default!;
    public PositionExitRiskCompletedEvent? CompletedEvent => latest?.CommandId == activeCommandId ? latest : null;
    public bool IsCompleted => CompletedEvent is not null;
    public long LastPersistedEventId { get; private set; }
    public PositionExitRiskFunctionState Prepare(EvaluatePositionExitRiskCommand request)
    {
        activeCommandId = request.CommandId;
        return this;
    }
    public bool Matches(EvaluatePositionExitRiskCommand request) =>
        CompletedEvent?.RequestFingerprint == request.InputHash;
    public bool TryComplete(PositionExitRiskCompletedEvent completed,
        EvaluatePositionExitRiskCommand request) => !IsCompleted && Update(completed, request);
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not PositionExitRiskCompletedEvent completed) return false;
        latest = completed;
        LastPersistedEventId = Math.Max(LastPersistedEventId, completed.EventId);
        return true;
    }
}
