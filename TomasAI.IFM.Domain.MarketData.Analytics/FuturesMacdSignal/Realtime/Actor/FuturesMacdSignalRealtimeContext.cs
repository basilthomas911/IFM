using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesMacdSignalRealtimeActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesMacdSignalRealtimeContext : EventActorContext, IFuturesMacdSignalRealtimeContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesMacdSignalRealtimeContext(
        IActorSupervisor supervisor,
        ILogger<FuturesMacdSignalRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesMacdSignalRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    /// <inheritdoc/>
    public ILogger<FuturesMacdSignalRealtimeActor> Logger { get; }
}
