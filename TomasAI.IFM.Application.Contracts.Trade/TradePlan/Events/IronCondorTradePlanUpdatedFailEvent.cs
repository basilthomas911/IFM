using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.TradePlan.Events
{

    public record IronCondorTradePlanUpdatedFailEvent : ErrorEvent
    {
    }

}
