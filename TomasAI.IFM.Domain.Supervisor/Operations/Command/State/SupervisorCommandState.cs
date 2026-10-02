using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Command.State;

/// <summary>Reconstructs the last committed Supervisor operation outcome for this command stream.</summary>
public sealed class SupervisorCommandState
    : BaseEventSourceActorState<SupervisorCommandState>,
      IEventSourceActorState<SupervisorCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; }

    /// <summary>Gets the last committed operation identity.</summary>
    public Guid LastOperationId { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not SupervisorActorOperationRecordedEvent outcome
            || outcome.CommandId == Guid.Empty)
            return false;
        LastOperationId = outcome.CommandId;
        return true;
    }
}
