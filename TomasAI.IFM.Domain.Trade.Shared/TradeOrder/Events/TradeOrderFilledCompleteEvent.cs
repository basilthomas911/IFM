using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.TradeOrder.Events
{


    public record TradeOrderFilledCompleteEvent : CompleteEvent
    {
        public TradeOrderEntityId TradeOrderId { get; init; }
        public TradeFillReadModel TradeFill { get; init; }
        public TradeOrderState TradeOrderState { get; init; }
        public DateTime FilledOn { get; init; }
        public string FilledBy { get; init; }
    }

}
