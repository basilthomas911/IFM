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

/// <summary>Provides the typed runtime context used by <see cref="EconomicCalendarQueryActor"/>.</summary>
public sealed class EconomicCalendarQueryContext : QueryActorContext, IQueryActorContext<EconomicCalendarQueryActor>, IEconomicCalendarQueryContext
{
    /// <summary>Initializes the context.</summary>
    public EconomicCalendarQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<EconomicCalendarQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, EconomicCalendarQueryActor.ActorName))
    { DbFactory = IsArgumentNull.Set(dbFactory); Logger = IsArgumentNull.Set(logger); }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<EconomicCalendarQueryActor> Logger { get; }
}
