using TomasAI.IFM.Domain.Trade.Shared;
using System;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.TradeOrder.Events
{
    public record BrokerOrderClosedEvent : ServiceEvent
    {
        public const int ErrorCode = 7032;

        public TradeOrderEntityId TradeOrderId { get; init; }
        public bool Executed { get; init; }
        public string ErrorMessage { get; init; }
        public DateTime ClosedOn { get; init; }
        public string ClosedBy { get; init; }
    }

}
