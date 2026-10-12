using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.TradeOrder.Events
{


    public record UpdateOrderExecutedCompleteEvent : CompleteEvent
    {
        public TradeOrderEntityId TradeOrderId { get; init; }
        public decimal OrderPrice { get; init; }
        public DateTime SubmittedOn { get; init; }
        public string SubmittedBy { get; init; }
    }

}
