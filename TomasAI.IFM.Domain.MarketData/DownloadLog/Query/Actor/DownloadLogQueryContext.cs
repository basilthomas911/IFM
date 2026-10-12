using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Query.Actor;

/// <summary>Provides the typed runtime context used by <see cref="DownloadLogQueryActor"/>.</summary>
public sealed class DownloadLogQueryContext : QueryActorContext, IDownloadLogQueryContext
{
    /// <summary>Initializes the context.</summary>
    public DownloadLogQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<DownloadLogQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, DownloadLogQueryActor.ActorName))
    { DbFactory = IsArgumentNull.Set(dbFactory); Logger = IsArgumentNull.Set(logger); }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<DownloadLogQueryActor> Logger { get; }
}
