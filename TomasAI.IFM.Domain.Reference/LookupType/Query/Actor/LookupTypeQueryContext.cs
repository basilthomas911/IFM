using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.LookupType.Query.Actor;

/// <summary>Provides the typed runtime context used by <see cref="LookupTypeQueryActor"/>.</summary>
public sealed class LookupTypeQueryContext :
    QueryActorContext,
    IQueryActorContext<LookupTypeQueryActor>,
    ILookupTypeQueryContext
{
    /// <summary>Initializes a lookup-type query context.</summary>
    public LookupTypeQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<LookupTypeQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, LookupTypeQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<LookupTypeQueryActor> Logger { get; }
}
