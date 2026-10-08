using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Futures.Command.State;

/// <summary>Owns authoritative EstablishedTradeDefinition state and pending source events.</summary>
public sealed class FuturesTradeCommandState : BaseEventSourceActorState<FuturesTradeCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public EstablishedTradeDefinition? EstablishedTradeDefinition { get; private set; }
    public EstablishedTradeDefinition? Current => EstablishedTradeDefinition;

    /// <summary>Applies supported source events to the authoritative business snapshot.</summary>
    protected override bool Apply(IEvent domainEvent)
    {
        switch (domainEvent)
        {
            case FuturesTradeChangedEvent changed:
                EstablishedTradeDefinition = changed.EstablishedTradeDefinition;
                return true;
            default:
                return false;
        }
    }
}
