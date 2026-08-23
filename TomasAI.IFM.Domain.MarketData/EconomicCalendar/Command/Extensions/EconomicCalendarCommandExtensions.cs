using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Extensions;

/// <summary>Exposes economic-calendar command services as readonly extension properties.</summary>
public static class EconomicCalendarCommandExtensions
{
    extension(ICommandActorContext<EconomicCalendarCommandActor> context)
    {
        /// <summary>Gets the domain context.</summary>
        public IEconomicCalendarCommandContext EconomicCalendarContext => IsArgumentNull.Set(context as IEconomicCalendarCommandContext)!;
        /// <summary>Gets the database factory.</summary>
        public IDbContextFactory DbFactory => context.EconomicCalendarContext.DbFactory;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<EconomicCalendarCommandActor> Logger => context.EconomicCalendarContext.Logger;
        /// <summary>Gets the event-source database.</summary>
        public IEventSourceActorDbContext DbEventSource => context.EconomicCalendarContext.DbEventSource;
        /// <summary>Gets the state factory.</summary>
        public IEventSourceActorStateFactory StateFactory => context.EconomicCalendarContext.StateFactory;
        /// <summary>Gets the actor service.</summary>
        public IActorService ActorService => context.EconomicCalendarContext.ActorService;
    }
}
