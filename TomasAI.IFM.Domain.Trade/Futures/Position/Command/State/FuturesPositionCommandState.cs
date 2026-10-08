using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;

/// <summary>Owns authoritative PositionSnapshot state and pending source events.</summary>
public sealed class FuturesPositionCommandState : BaseEventSourceActorState<FuturesPositionCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public StrategyPositionSnapshot? PositionSnapshot { get; private set; }
    public StrategyPositionSnapshot? Current => PositionSnapshot;

    /// <summary>Applies supported source events to the authoritative business snapshot.</summary>
    protected override bool Apply(IEvent domainEvent)
    {
        switch (domainEvent)
        {
            case FuturesPositionChangedEvent changed:
                PositionSnapshot = changed.PositionSnapshot;
                return true;
            default:
                return false;
        }
    }
}
