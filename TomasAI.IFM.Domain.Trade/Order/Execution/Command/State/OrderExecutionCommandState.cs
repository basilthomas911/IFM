using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;

/// <summary>Owns the authoritative definition of one execution attempt.</summary>
public sealed class OrderExecutionCommandState : BaseEventSourceActorState<OrderExecutionCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public OrderExecutionDefinition? OrderExecutionDefinition { get; private set; }

    /// <summary>Mutates execution data only from a valid source event.</summary>
    /// <param name="domainEvent">The event to apply or reconstruct.</param>
    /// <returns>True when event ownership is valid.</returns>
    protected override bool Apply(IEvent domainEvent)
    {
        switch (domainEvent)
        {
            case OrderExecutionChangedEvent changed:
                if (!changed.EntityId.IsValid || changed.OrderExecutionDefinition.Id != changed.EntityId ||
                    !changed.OrderExecutionDefinition.TradeOrderId.IsValid) return false;
                OrderExecutionDefinition = changed.OrderExecutionDefinition;
                return true;
            default: return false;
        }
    }
}
