using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesAdxSignalRealtimeActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesAdxSignalRealtimeContext : EventActorContext, IFuturesAdxSignalRealtimeContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesAdxSignalRealtimeContext(
        IActorSupervisor supervisor,
        ILogger<FuturesAdxSignalRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesAdxSignalRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<FuturesAdxSignalRealtimeActor> Logger { get; }
}
