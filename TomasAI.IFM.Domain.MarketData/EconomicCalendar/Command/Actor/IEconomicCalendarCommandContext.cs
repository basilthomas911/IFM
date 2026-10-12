using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Actor;

/// <summary>Defines the runtime services required by <see cref="EconomicCalendarCommandActor"/>.</summary>
public interface IEconomicCalendarCommandContext : ICommandActorContext<EconomicCalendarCommandActor>
{
    /// <summary>Gets the database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<EconomicCalendarCommandActor> Logger { get; }
    /// <summary>Gets the event-source database context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the state factory.</summary>
    IEventSourceActorStateFactory StateFactory { get; }
    /// <summary>Gets the actor service.</summary>
    IActorService ActorService { get; }
}
