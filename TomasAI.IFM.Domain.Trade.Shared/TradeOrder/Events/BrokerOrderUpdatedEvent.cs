using TomasAI.IFM.Shared.Trade;
using System;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.TradeOrder.Events
{
    public record BrokerOrderUpdatedEvent : ServiceEvent
    {
        public const int ErrorCode = 7035;

        public TradeOrderEntityId TradeOrderId { get; init; }
        public bool Executed { get; init; }
        public string ErrorMessage { get; init; }
        public DateTime SubmittedOn { get; init; }
        public string SubmittedBy { get; init; }
    }

}
