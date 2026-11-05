using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command.State;

/// <summary>Reconstructs the latest durable account, gate, hold, and qualification state.</summary>
public sealed class BrokerAccountCommandState : BaseEventSourceActorState<BrokerAccountCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;
    /// <summary>Gets the authoritative account reconstructed exclusively from account events.</summary>
    public BrokerAccountDefinition? BrokerAccountDefinition { get; private set; }

    /// <summary>Dispatches account events and mutates only the owning account definition.</summary>
    protected override bool Apply(IEvent domainEvent) => ApplyCheckpointEvent(domainEvent, false);

    internal bool ApplyCheckpointEvent(IEvent domainEvent, bool checkpoint) =>
        domainEvent switch
        {
            BrokerAccountOperationDecidedEvent decision => OwnsAccount(decision.EntityId),
            BrokerAccountResynchronizationRequestedEvent request => OwnsAccount(request.EntityId),
            BrokerAccountChangedEvent changed => ApplyAccountChange(changed, checkpoint),
            _ => false
        };

    private bool OwnsAccount(BrokerAccountId accountId) => accountId.IsValid &&
        (BrokerAccountDefinition is null || BrokerAccountDefinition.Id == accountId);

    /// <summary>Applies an account event only when its identity and next revision are valid.</summary>
    private bool ApplyAccountChange(BrokerAccountChangedEvent changed, bool checkpoint)
    {
        if (!changed.EntityId.IsValid || changed.BrokerAccountDefinition.Id != changed.EntityId ||
            BrokerAccountDefinition is { } current &&
            (checkpoint ? changed.BrokerAccountDefinition.Revision <= current.Revision
                : changed.BrokerAccountDefinition.Revision != current.Revision + 1))
            return false;
        BrokerAccountDefinition = changed.BrokerAccountDefinition;
        return true;
    }
}
