using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Events
{


    public record TradePlanForwardLossLimitClearedCompleteEvent : CompleteEvent
    {
        public TradePlanForwardLossLimitEntityId ForwardLossLimitId { get; init; }
        public DateTime ClearedOn { get; init; }
        public string ClearedBy { get; init; }

    }
}
