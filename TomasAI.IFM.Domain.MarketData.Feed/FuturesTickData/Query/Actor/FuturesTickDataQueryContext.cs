using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesTickDataQueryActor"/>.</summary>
public interface IFuturesTickDataQueryContext : IQueryActorContext<FuturesTickDataQueryActor>
{
    /// <summary>Gets the database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesTickDataQueryActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesTickDataQueryActor"/>.</summary>
public sealed class FuturesTickDataQueryContext : QueryActorContext, IQueryActorContext<FuturesTickDataQueryActor>, IFuturesTickDataQueryContext
{
    /// <summary>Initializes the typed query context.</summary>
    public FuturesTickDataQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<FuturesTickDataQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, FuturesTickDataQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<FuturesTickDataQueryActor> Logger { get; }
}

