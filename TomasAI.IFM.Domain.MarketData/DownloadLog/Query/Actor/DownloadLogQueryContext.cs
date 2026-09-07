using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Query.Actor;

/// <summary>Defines the runtime services required by <see cref="DownloadLogQueryActor"/>.</summary>
public interface IDownloadLogQueryContext : IQueryActorContext<DownloadLogQueryActor>
{
    /// <summary>Gets the database factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<DownloadLogQueryActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="DownloadLogQueryActor"/>.</summary>
public sealed class DownloadLogQueryContext : QueryActorContext, IQueryActorContext<DownloadLogQueryActor>, IDownloadLogQueryContext
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
