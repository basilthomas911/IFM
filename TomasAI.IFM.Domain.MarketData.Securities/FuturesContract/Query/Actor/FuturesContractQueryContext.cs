using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesContractQueryActor"/>.</summary>
public sealed class FuturesContractQueryContext : QueryActorContext,
    IQueryActorContext<FuturesContractQueryActor>, IFuturesContractQueryContext
{
    /// <summary>Initializes a futures-contract query context.</summary>
    public FuturesContractQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
        ILogger<FuturesContractQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, FuturesContractQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<FuturesContractQueryActor> Logger { get; }
}
