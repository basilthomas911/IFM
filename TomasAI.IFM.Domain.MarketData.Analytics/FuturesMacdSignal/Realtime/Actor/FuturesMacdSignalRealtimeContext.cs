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

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesMacdSignalRealtimeActor"/>.</summary>
public interface IFuturesMacdSignalRealtimeContext : IRealtimeActorContext<FuturesMacdSignalRealtimeActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the Projector service supplied to the actor context.</summary>
    IRealtimeProjector<FuturesMacdSignalRealtimeActor> Projector { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesMacdSignalRealtimeActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesMacdSignalRealtimeActor"/>.</summary>
public sealed class FuturesMacdSignalRealtimeContext : EventActorContext, IRealtimeActorContext<FuturesMacdSignalRealtimeActor>, IFuturesMacdSignalRealtimeContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesMacdSignalRealtimeContext(
        IActorSupervisor supervisor,
        IRealtimeProjector<FuturesMacdSignalRealtimeActor> projector,
        ILogger<FuturesMacdSignalRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesMacdSignalRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Projector = IsArgumentNull.Set(projector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IRealtimeProjector<FuturesMacdSignalRealtimeActor> Projector { get; }
    /// <inheritdoc/>
    public ILogger<FuturesMacdSignalRealtimeActor> Logger { get; }
}
