using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.Shared.Events
{


    public record StrikePriceVolatilityAddedCompleteEvent : CompleteEvent
    {
        public StrikePriceVolatilityReadModel StrikePriceVolatility { get; init; }
        public DateTime CreatedOn { get; init; }
        public string CreatedBy { get; init; }

    }

}
