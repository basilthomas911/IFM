using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesOptionContractQueryActor"/>.</summary>
public sealed class FuturesOptionContractQueryContext : QueryActorContext,
    IQueryActorContext<FuturesOptionContractQueryActor>, IFuturesOptionContractQueryContext
{
    /// <summary>Initializes a futures-option-contract query context.</summary>
    public FuturesOptionContractQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
        ILogger<FuturesOptionContractQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, FuturesOptionContractQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<FuturesOptionContractQueryActor> Logger { get; }
}
