using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.TradeOrder.Events
{

    public record TradeOrderPlacedFailEvent : ErrorEvent
    {
    }

}
