using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Events
{


    public record TradePlanForwardLossLimitWarningUpdatedCompleteEvent : CompleteEvent
    {
        public TradePlanForwardLossLimitReadModel TradePlanForwardLossLimit { get; init; }
        public DateTime UpdatedOn { get; init; }
        public string UpdatedBy { get; init; }

    }
}
