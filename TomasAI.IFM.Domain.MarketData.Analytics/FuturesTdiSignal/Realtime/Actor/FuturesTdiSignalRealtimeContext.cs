using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesTdiSignalRealtimeActor"/>.</summary>
public interface IFuturesTdiSignalRealtimeContext : IRealtimeActorContext<FuturesTdiSignalRealtimeActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the Projector service supplied to the actor context.</summary>
    IRealtimeProjector<FuturesTdiSignalRealtimeActor> Projector { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesTdiSignalRealtimeActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesTdiSignalRealtimeActor"/>.</summary>
public sealed class FuturesTdiSignalRealtimeContext : EventActorContext, IRealtimeActorContext<FuturesTdiSignalRealtimeActor>, IFuturesTdiSignalRealtimeContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesTdiSignalRealtimeContext(
        IActorSupervisor supervisor,
        IRealtimeProjector<FuturesTdiSignalRealtimeActor> projector,
        ILogger<FuturesTdiSignalRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesTdiSignalRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Projector = IsArgumentNull.Set(projector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IRealtimeProjector<FuturesTdiSignalRealtimeActor> Projector { get; }
    /// <inheritdoc/>
    public ILogger<FuturesTdiSignalRealtimeActor> Logger { get; }
}
