using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Command.State;

/// <summary>Owns authoritative Trade Order data and applies its source events.</summary>
public sealed class TradeOrderCommandState : BaseEventSourceActorState<TradeOrderCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public TradeOrderDefinition? TradeOrderDefinition { get; private set; }
    /// <summary>Mutates the owned definition only from a supported source event.</summary>
    /// <param name="domainEvent">The new or replayed source event.</param>
    /// <returns>True when the supported event was applied; otherwise, false.</returns>
    protected override bool Apply(IEvent domainEvent)
    {
        switch (domainEvent)
        {
            case TradeOrderChangedEvent tradeOrderChanged:
                TradeOrderDefinition = tradeOrderChanged.TradeOrderDefinition;
                return true;
            default:
                return false;
        }
    }
}
