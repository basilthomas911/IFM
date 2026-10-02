using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Command.State;

/// <summary>Reconstructs the last committed recovery-canary proof identity.</summary>
public sealed class RecoveryCanaryCommandState
    : BaseEventSourceActorState<RecoveryCanaryCommandState>,
      IEventSourceActorState<RecoveryCanaryCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; }

    /// <summary>Gets the last committed canary correlation identity.</summary>
    public Guid LastCorrelationId { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not RecoveryCanaryRecordedEvent recorded
            || recorded.CorrelationId == Guid.Empty)
            return false;
        LastCorrelationId = recorded.CorrelationId;
        return true;
    }
}
