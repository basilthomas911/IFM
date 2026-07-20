using System;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.TradeOrder.Events
{
    public record BrokerOrderOpenedEvent : ServiceEvent
    {
        public const int ErrorCode = 7034;

        public TradeOrderEntityId TradeOrderId { get; init; }
        public bool Executed { get; init; }
        public string ErrorMessage { get; init; }
        public DateTime OpenedOn { get; init; }
        public string OpenedBy { get; init; }
    }

}
