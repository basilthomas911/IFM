using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesOptionTickDataQueryActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesOptionTickDataQueryContext : QueryActorContext, IFuturesOptionTickDataQueryContext
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

