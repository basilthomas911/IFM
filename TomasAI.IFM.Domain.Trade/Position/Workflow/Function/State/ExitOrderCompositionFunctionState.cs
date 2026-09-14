using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Position.Workflow.Function.State;

public sealed class ExitOrderCompositionFunctionState : BaseEventSourceActorState<ExitOrderCompositionFunctionState>,
    IEventSourceFunctionState<ExitOrderCompositionFunctionState, ComposeExitOrderCommand,
        ExitOrderCompositionCompletedEvent>
{
    Guid activeCommandId;
    ExitOrderCompositionCompletedEvent? latest;
    public override ActorThreadId Id { get; set; } = default!;
    public ExitOrderCompositionCompletedEvent? CompletedEvent =>
        latest?.CommandId == activeCommandId ? latest : null;
    public bool IsCompleted => CompletedEvent is not null;
    public long LastPersistedEventId { get; private set; }
    public ExitOrderCompositionFunctionState Prepare(ComposeExitOrderCommand request)
    {
        activeCommandId = request.CommandId;
        return this;
    }
    public bool Matches(ComposeExitOrderCommand request) =>
        CompletedEvent?.RequestFingerprint == request.InputHash;
    public bool TryComplete(ExitOrderCompositionCompletedEvent completed,
        ComposeExitOrderCommand request) => !IsCompleted && Update(completed, request);
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not ExitOrderCompositionCompletedEvent completed) return false;
        latest = completed;
        LastPersistedEventId = Math.Max(LastPersistedEventId, completed.EventId);
        return true;
    }
}
