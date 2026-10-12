using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Realtime.Actor;

/// <summary>Provides the typed EMA realtime context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesEmaSignalRealtimeContext : EventActorContext,
IFuturesEmaSignalRealtimeContext
{
    /// <summary>Initializes the context.</summary>
    public FuturesEmaSignalRealtimeContext(IActorSupervisor supervisor,
        ILogger<FuturesEmaSignalRealtimeActor> logger)
        : base(supervisor, new(ActorType.Realtime, FuturesEmaSignalRealtimeActor.ActorName)) =>
        Logger = IsArgumentNull.Set(logger);
    /// <summary>Gets the actor logger.</summary>
    public ILogger<FuturesEmaSignalRealtimeActor> Logger { get; }
}
