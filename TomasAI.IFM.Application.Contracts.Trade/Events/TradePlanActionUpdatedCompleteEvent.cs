using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Events
{


    public record TradePlanActionUpdatedCompleteEvent : CompleteEvent
    {
        public TradePlanActionReadModel TradePlanAction { get; init; }
    }

}
