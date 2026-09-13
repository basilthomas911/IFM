using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;

public sealed class FuturesPositionCommandState : BaseEventSourceActorState<FuturesPositionCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public StrategyPositionSnapshot? Current { get; private set; }

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not FuturesPositionChangedEvent changed) return false;
        Current = changed.State;
        return true;
    }
}
