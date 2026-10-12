using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query.Actor;

/// <summary>Defines the runtime services required by <see cref="EconomicCalendarQueryActor"/>.</summary>
public interface IEconomicCalendarQueryContext : IQueryActorContext<EconomicCalendarQueryActor>
{
    /// <summary>Gets the database factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<EconomicCalendarQueryActor> Logger { get; }
}
