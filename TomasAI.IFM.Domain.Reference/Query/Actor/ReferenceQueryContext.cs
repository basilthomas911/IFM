using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.Query.Actor;

/// <summary>Provides the typed runtime context used by <see cref="ReferenceQueryActor"/>.</summary>
public sealed class ReferenceQueryContext :
    QueryActorContext,
    IQueryActorContext<ReferenceQueryActor>,
    IReferenceQueryContext
{
    /// <summary>Initializes a Reference query context.</summary>
    public ReferenceQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<ReferenceQueryActor> logger,
        TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi? marketDataApi = null)
        : base(supervisor, new ActorMailboxId(ActorType.Query, ReferenceQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
        MarketDataApi = marketDataApi;
    }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<ReferenceQueryActor> Logger { get; }
    public TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi? MarketDataApi { get; }
}
