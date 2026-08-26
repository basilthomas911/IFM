using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesAdxSignalRealtimeActor"/>.</summary>
public interface IFuturesAdxSignalRealtimeContext : IRealtimeActorContext<FuturesAdxSignalRealtimeActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesAdxSignalRealtimeActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesAdxSignalRealtimeActor"/>.</summary>
public sealed class FuturesAdxSignalRealtimeContext : EventActorContext, IRealtimeActorContext<FuturesAdxSignalRealtimeActor>, IFuturesAdxSignalRealtimeContext
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
