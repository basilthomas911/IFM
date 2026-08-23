using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesBarDataQueryActor"/>.</summary>
public interface IFuturesBarDataQueryContext : IQueryActorContext<FuturesBarDataQueryActor>
{
    /// <summary>Gets the database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesBarDataQueryActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesBarDataQueryActor"/>.</summary>
public sealed class FuturesBarDataQueryContext : QueryActorContext, IQueryActorContext<FuturesBarDataQueryActor>, IFuturesBarDataQueryContext
{
    /// <summary>Initializes the typed query context.</summary>
    public FuturesBarDataQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<FuturesBarDataQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, FuturesBarDataQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<FuturesBarDataQueryActor> Logger { get; }
}

