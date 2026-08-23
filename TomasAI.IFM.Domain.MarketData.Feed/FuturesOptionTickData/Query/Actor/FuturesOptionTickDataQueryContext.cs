using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesOptionTickDataQueryActor"/>.</summary>
public interface IFuturesOptionTickDataQueryContext : IQueryActorContext<FuturesOptionTickDataQueryActor>
{
    /// <summary>Gets the database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesOptionTickDataQueryActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesOptionTickDataQueryActor"/>.</summary>
public sealed class FuturesOptionTickDataQueryContext : QueryActorContext, IQueryActorContext<FuturesOptionTickDataQueryActor>, IFuturesOptionTickDataQueryContext
{
    /// <summary>Initializes the typed query context.</summary>
    public FuturesOptionTickDataQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<FuturesOptionTickDataQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<FuturesOptionTickDataQueryActor> Logger { get; }
}

