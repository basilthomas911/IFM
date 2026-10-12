using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Event.Actor;

/// <summary>Provides the typed EMA event context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesEmaSignalEventContext : EventActorContext,
IFuturesEmaSignalEventContext
{
    /// <summary>Initializes the context.</summary>
    public FuturesEmaSignalEventContext(IActorSupervisor supervisor,
        ILogger<FuturesEmaSignalEventActor> logger)
        : base(supervisor, new(ActorType.Event, FuturesEmaSignalEventActor.ActorName)) =>
        Logger = IsArgumentNull.Set(logger);
    /// <summary>Gets the actor logger.</summary>
    public ILogger<FuturesEmaSignalEventActor> Logger { get; }
}
