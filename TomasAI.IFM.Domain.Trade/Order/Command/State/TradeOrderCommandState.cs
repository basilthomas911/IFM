using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Command.State;

public sealed class TradeOrderCommandState : BaseEventSourceActorState<TradeOrderCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public TradeOrderDefinition? Current { get; private set; }
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not TradeOrderChangedEvent changed) return false;
        Current = changed.State;
        return true;
    }
}
