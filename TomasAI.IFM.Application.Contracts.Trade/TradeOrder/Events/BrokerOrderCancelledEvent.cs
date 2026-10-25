using TomasAI.IFM.Domain.Trade.Shared;
using System;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.TradeOrder.Events
{
    public record BrokerOrderCancelledEvent : ServiceEvent
    {
        public const int ErrorCode = 7031;

        public TradeOrderEntityId TradeOrderId { get; init; }
        public bool Executed { get; init; }
        public string ErrorMessage { get; init; }
        public DateTime SubmittedOn { get; init; }
        public string SubmittedBy { get; init; }
    }

}
