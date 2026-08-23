using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event.Extensions;

/// <summary>Exposes the economic-calendar event context.</summary>
public static class EconomicCalendarEventExtensions
{
    extension(IEventActorContext<EconomicCalendarEventActor> context)
    {
        /// <summary>Gets the domain context.</summary>
        public IEconomicCalendarEventContext EconomicCalendarContext => IsArgumentNull.Set(context as IEconomicCalendarEventContext)!;
    }
}
