using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Events
{


    public record TradePositionOpenedCompleteEvent : CompleteEvent
    {
        public TradePositionEntityId TradePositionId { get; init; }
        public TradePositionActionReadModel TradePositionAction { get; init; }
        public DateTime CreatedOn { get; init; }
        public string CreatedBy { get; init; }
    }

}
