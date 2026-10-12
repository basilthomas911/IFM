using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.Events
{

    public record StrikePriceVolatilityRemovedFailEvent : ErrorEvent
    {
    }

}
