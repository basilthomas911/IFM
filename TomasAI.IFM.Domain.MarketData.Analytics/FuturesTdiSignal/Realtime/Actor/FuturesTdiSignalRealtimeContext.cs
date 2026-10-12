using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesTdiSignalRealtimeActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesTdiSignalRealtimeContext : EventActorContext, IFuturesTdiSignalRealtimeContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesTdiSignalRealtimeContext(
        IActorSupervisor supervisor,
        IRealtimeProjector<FuturesTdiSignalRealtimeActor> projector,
        IDbContextFactory dbFactory,
        ILogger<FuturesTdiSignalRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesTdiSignalRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Projector = IsArgumentNull.Set(projector);
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IRealtimeProjector<FuturesTdiSignalRealtimeActor> Projector { get; }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<FuturesTdiSignalRealtimeActor> Logger { get; }
}
