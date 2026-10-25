using TomasAI.IFM.Domain.Trade.Shared;
using System;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.TradeOrder.Events
{
    public record BrokerOrderFilledEvent : ServiceEvent
    {
        public const int ErrorCode = 7032;

        public TradeOrderEntityId TradeOrderId { get; init; }
        public TradeFillReadModel TradeFill { get; init; }
        public bool Executed { get; init; }
        public string ErrorMessage { get; init; }
        public DateTime FilledOn { get; init; }
        public string FilledBy { get; init; }
    }

}
