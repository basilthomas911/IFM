using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Transaction.Query.Actor;

/// <summary>
/// Provides the shared query runtime context and Fund transaction services required by
/// <see cref="FundTransactionQueryActor"/>.
/// </summary>
public sealed class FundTransactionQueryContext :
    QueryActorContext,
    IQueryActorContext<FundTransactionQueryActor>,
    IFundTransactionQueryContext
{
    /// <summary>Initializes a Fund transaction query context.</summary>
    /// <param name="supervisor">The actor supervisor that owns the query actor.</param>
    /// <param name="dbFactory">The database-context factory used by Fund transaction queries.</param>
    /// <param name="logger">The logger associated with the query actor.</param>
    public FundTransactionQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<FundTransactionQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, FundTransactionQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }

    /// <inheritdoc/>
    public ILogger<FundTransactionQueryActor> Logger { get; }
}
