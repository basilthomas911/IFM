using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Events
{


    public record TradePositionAddedCompleteEvent : CompleteEvent
    {
        public int OrderId { get; init; }
        public decimal AssetPrice { get; init; }
        public double RiskFreeRate { get; init; }
        public TradePositionReadModel TradePosition { get; init; }
        public DateTime CreatedOn { get; init; }
        public string CreatedBy { get; init; }

    }


}
