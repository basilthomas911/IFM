using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.TradeOrder.Events
{


    public record TradeOrderUpdatedCompleteEvent : CompleteEvent
    {
        public TradeOrderEntityId TradeOrderId { get; init; }
        public decimal OrderPrice { get; init; }
        public DateTime UpdatedOn { get; init; }
        public string UpdatedBy { get; init; }
    }

}
