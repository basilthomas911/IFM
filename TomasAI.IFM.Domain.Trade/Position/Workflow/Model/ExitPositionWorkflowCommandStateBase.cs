using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Position.Workflow.Model;

/// <summary>Rehydrates the durable start of one exit decision.</summary>
public abstract class ExitPositionWorkflowCommandStateBase<TState> : BaseEventSourceActorState<TState>
    where TState : ExitPositionWorkflowCommandStateBase<TState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public ExitPositionWorkflowStartedEvent? Started { get; private set; }

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not ExitPositionWorkflowStartedEvent started)
            return false;

        Started = started;
        return true;
    }
}
