using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesAtrSignalRealtimeActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesAtrSignalRealtimeContext : EventActorContext, IFuturesAtrSignalRealtimeContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesAtrSignalRealtimeContext(
        IActorSupervisor supervisor,
        ILogger<FuturesAtrSignalRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesAtrSignalRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    /// <inheritdoc/>
    public ILogger<FuturesAtrSignalRealtimeActor> Logger { get; }
}
