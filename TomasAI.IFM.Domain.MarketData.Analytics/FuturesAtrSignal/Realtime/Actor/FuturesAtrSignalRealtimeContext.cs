using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesAtrSignalRealtimeActor"/>.</summary>
public interface IFuturesAtrSignalRealtimeContext : IRealtimeActorContext<FuturesAtrSignalRealtimeActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the Projector service supplied to the actor context.</summary>
    IRealtimeProjector<FuturesAtrSignalRealtimeActor> Projector { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesAtrSignalRealtimeActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesAtrSignalRealtimeActor"/>.</summary>
public sealed class FuturesAtrSignalRealtimeContext : EventActorContext, IRealtimeActorContext<FuturesAtrSignalRealtimeActor>, IFuturesAtrSignalRealtimeContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesAtrSignalRealtimeContext(
        IActorSupervisor supervisor,
        IRealtimeProjector<FuturesAtrSignalRealtimeActor> projector,
        ILogger<FuturesAtrSignalRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesAtrSignalRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Projector = IsArgumentNull.Set(projector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IRealtimeProjector<FuturesAtrSignalRealtimeActor> Projector { get; }
    /// <inheritdoc/>
    public ILogger<FuturesAtrSignalRealtimeActor> Logger { get; }
}
