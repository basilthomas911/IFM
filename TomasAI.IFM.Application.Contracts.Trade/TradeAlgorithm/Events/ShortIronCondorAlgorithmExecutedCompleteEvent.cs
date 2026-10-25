using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.TradeAlgorithm.Events
{


    public record ShortIronCondorAlgorithmExecutedCompleteEvent : CompleteEvent
    {
        public TradeAlgorithmId TradeAlgorithmId { get; init; }
        public TradePlanReadModel TradePlan { get; init; }
    }


}
