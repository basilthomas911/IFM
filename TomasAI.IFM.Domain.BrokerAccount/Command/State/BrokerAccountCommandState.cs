using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command.State;

/// <summary>Reconstructs the latest durable account, gate, hold, and qualification state.</summary>
public sealed class BrokerAccountCommandState : BaseEventSourceActorState<BrokerAccountCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public BrokerAccountDefinition? Current { get; private set; }

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not BrokerAccountChangedEvent changed || !changed.EntityId.IsValid ||
            changed.State.Id != changed.EntityId ||
            Current is not null && changed.State.Revision != Current.Revision + 1)
            return false;
        Current = changed.State;
        return true;
    }
}
