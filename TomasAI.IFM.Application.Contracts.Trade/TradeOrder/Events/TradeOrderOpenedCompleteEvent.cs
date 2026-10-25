using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.TradeOrder.Events;


public record TradeOrderOpenedCompleteEvent : CompleteEvent
{
    public TradeOrderEntityId TradeOrderId { get; init; }
    public DateTime OpenedOn { get; init; }
    public string OpenedBy { get; init; }
}
