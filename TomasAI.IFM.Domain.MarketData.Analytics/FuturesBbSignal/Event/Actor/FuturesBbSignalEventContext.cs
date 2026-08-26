using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Event.Actor;

/// <summary>Defines the readonly Bollinger event context.</summary>
public interface IFuturesBbSignalEventContext : IEventActorContext<FuturesBbSignalEventActor>
{
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesBbSignalEventActor> Logger { get; }
}

/// <summary>Provides the typed Bollinger event context.</summary>
public sealed class FuturesBbSignalEventContext : EventActorContext,
    IEventActorContext<FuturesBbSignalEventActor>, IFuturesBbSignalEventContext
{
    /// <summary>Initializes the context.</summary>
    public FuturesBbSignalEventContext(IActorSupervisor supervisor,
        ILogger<FuturesBbSignalEventActor> logger)
        : base(supervisor, new(ActorType.Event, FuturesBbSignalEventActor.ActorName)) =>
        Logger = IsArgumentNull.Set(logger);
    /// <summary>Gets the actor logger.</summary>
    public ILogger<FuturesBbSignalEventActor> Logger { get; }
}
