using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="EconomicCalendarEventActor"/>.</summary>
public sealed class EconomicCalendarEventContext : EventActorContext, IEconomicCalendarEventContext
{
    /// <summary>Initializes the context.</summary>
    public EconomicCalendarEventContext(IActorSupervisor supervisor, IReferenceDataApi referenceDataApi,
        IDbContextFactory dbFactory, ILogger<EconomicCalendarEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, EconomicCalendarEventActor.Actor))
    { Supervisor = IsArgumentNull.Set(supervisor); ReferenceDataApi = IsArgumentNull.Set(referenceDataApi); DbFactory = IsArgumentNull.Set(dbFactory); Logger = IsArgumentNull.Set(logger); }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IReferenceDataApi ReferenceDataApi { get; }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<EconomicCalendarEventActor> Logger { get; }
}
