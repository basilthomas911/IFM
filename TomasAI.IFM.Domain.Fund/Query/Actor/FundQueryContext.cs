using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Query.Actor;

/// <summary>
/// Provides the shared query actor runtime context and Fund-specific services required by <see cref="FundQueryActor"/>.
/// </summary>
public sealed class FundQueryContext :
    QueryActorContext,
    IQueryActorContext<FundQueryActor>,
    IFundQueryContext
{
    /// <summary>
    /// Initializes a Fund query context.
    /// </summary>
    /// <param name="supervisor">The actor supervisor that owns the Fund query actor.</param>
    /// <param name="dbFactory">The database-context factory used by Fund queries.</param>
    /// <param name="logger">The logger associated with <see cref="FundQueryActor"/>.</param>
    public FundQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<FundQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, FundQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }

    /// <inheritdoc/>
    public ILogger<FundQueryActor> Logger { get; }
}
