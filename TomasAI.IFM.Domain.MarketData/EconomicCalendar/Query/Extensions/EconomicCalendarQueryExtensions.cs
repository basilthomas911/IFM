using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query.Extensions;

/// <summary>Exposes economic-calendar query services as readonly extension properties.</summary>
public static class EconomicCalendarQueryExtensions
{
    extension(IQueryActorContext<EconomicCalendarQueryActor> context)
    {
        /// <summary>Gets the domain context.</summary>
        public IEconomicCalendarQueryContext EconomicCalendarContext => IsArgumentNull.Set(context as IEconomicCalendarQueryContext)!;
        /// <summary>Gets the database factory.</summary>
        public IDbContextFactory DbFactory => context.EconomicCalendarContext.DbFactory;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<EconomicCalendarQueryActor> Logger => context.EconomicCalendarContext.Logger;
    }
}
