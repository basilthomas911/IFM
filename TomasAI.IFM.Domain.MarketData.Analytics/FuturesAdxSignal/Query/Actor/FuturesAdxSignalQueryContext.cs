using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesAdxSignalQueryActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesAdxSignalQueryContext : QueryActorContext, IFuturesAdxSignalQueryContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesAdxSignalQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<FuturesAdxSignalQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, FuturesAdxSignalQueryActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<FuturesAdxSignalQueryActor> Logger { get; }
}
