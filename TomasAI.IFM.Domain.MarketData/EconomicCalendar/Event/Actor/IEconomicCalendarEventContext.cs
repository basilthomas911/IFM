using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event.Actor;

/// <summary>Defines the runtime services required by <see cref="EconomicCalendarEventActor"/>.</summary>
public interface IEconomicCalendarEventContext : IEventActorContext<EconomicCalendarEventActor>
{
    /// <summary>Gets the supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the reference-data API.</summary>
    IReferenceDataApi ReferenceDataApi { get; }
    /// <summary>Gets the database factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<EconomicCalendarEventActor> Logger { get; }
}
