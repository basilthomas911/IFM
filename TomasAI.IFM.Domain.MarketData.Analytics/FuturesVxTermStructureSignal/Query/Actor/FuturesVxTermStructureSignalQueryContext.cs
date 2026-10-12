using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query.Actor;

/// <summary>Provides the typed VX term-structure Query context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesVxTermStructureSignalQueryContext : QueryActorContext,
    IFuturesVxTermStructureSignalQueryContext
{
    /// <summary>Initializes the typed context.</summary>
    public FuturesVxTermStructureSignalQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<FuturesVxTermStructureSignalQueryActor> logger)
        : base(supervisor, new(ActorType.Query, FuturesVxTermStructureSignalQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc />
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc />
    public ILogger<FuturesVxTermStructureSignalQueryActor> Logger { get; }
}
