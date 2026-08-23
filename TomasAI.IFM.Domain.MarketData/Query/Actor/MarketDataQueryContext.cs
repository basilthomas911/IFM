using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Query.Actor;

/// <summary>Defines the runtime services required by <see cref="MarketDataQueryActor"/>.</summary>
public interface IMarketDataQueryContext : IQueryActorContext<MarketDataQueryActor>
{
    /// <summary>Gets the database factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<MarketDataQueryActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="MarketDataQueryActor"/>.</summary>
public sealed class MarketDataQueryContext : QueryActorContext, IQueryActorContext<MarketDataQueryActor>, IMarketDataQueryContext
{
    /// <summary>Initializes the context.</summary>
    public MarketDataQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<MarketDataQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, MarketDataQueryActor.ActorName))
    { DbFactory = IsArgumentNull.Set(dbFactory); Logger = IsArgumentNull.Set(logger); }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<MarketDataQueryActor> Logger { get; }
}
