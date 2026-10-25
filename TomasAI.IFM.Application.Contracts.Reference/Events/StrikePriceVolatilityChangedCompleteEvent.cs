using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.Shared.Events
{


    public record StrikePriceVolatilityChangedCompleteEvent : CompleteEvent
    {
        public StrikePriceVolatilityId OriginalId { get; init; }
        public StrikePriceVolatilityReadModel StrikePriceVolatility { get; init; }
        public DateTime UpdatedOn { get; init; }
        public string UpdatedBy { get; init; }

    }

}
