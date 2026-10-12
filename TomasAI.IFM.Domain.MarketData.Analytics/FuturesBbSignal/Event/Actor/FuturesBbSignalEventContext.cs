using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Event.Actor;

/// <summary>Provides the typed Bollinger event context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesBbSignalEventContext : EventActorContext,
IFuturesBbSignalEventContext
{
    /// <summary>Initializes the context.</summary>
    public FuturesBbSignalEventContext(IActorSupervisor supervisor,
        ILogger<FuturesBbSignalEventActor> logger)
        : base(supervisor, new(ActorType.Event, FuturesBbSignalEventActor.ActorName)) =>
        Logger = IsArgumentNull.Set(logger);
    /// <summary>Gets the actor logger.</summary>
    public ILogger<FuturesBbSignalEventActor> Logger { get; }
}
