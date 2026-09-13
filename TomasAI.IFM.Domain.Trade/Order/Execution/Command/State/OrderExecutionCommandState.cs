using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;

public sealed class OrderExecutionCommandState : BaseEventSourceActorState<OrderExecutionCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public OrderExecutionDefinition? Current { get; private set; }
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not OrderExecutionChangedEvent changed) return false;
        Current=changed.State; return true;
    }
}
