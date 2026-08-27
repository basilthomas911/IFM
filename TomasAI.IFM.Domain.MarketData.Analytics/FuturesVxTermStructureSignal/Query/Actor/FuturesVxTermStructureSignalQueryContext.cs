using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query.Actor;

/// <summary>Defines readonly services required by the VX term-structure Query actor.</summary>
public interface IFuturesVxTermStructureSignalQueryContext
    : IQueryActorContext<FuturesVxTermStructureSignalQueryActor>
{
    IDbContextFactory DbFactory { get; }
    ILogger<FuturesVxTermStructureSignalQueryActor> Logger { get; }
}

/// <summary>Provides the typed VX term-structure Query context.</summary>
public sealed class FuturesVxTermStructureSignalQueryContext : QueryActorContext,
    IQueryActorContext<FuturesVxTermStructureSignalQueryActor>,
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
