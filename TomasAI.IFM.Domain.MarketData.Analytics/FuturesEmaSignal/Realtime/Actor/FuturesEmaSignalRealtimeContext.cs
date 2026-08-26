using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Realtime.Actor;

/// <summary>Defines the readonly EMA realtime context.</summary>
public interface IFuturesEmaSignalRealtimeContext : IRealtimeActorContext<FuturesEmaSignalRealtimeActor>
{
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesEmaSignalRealtimeActor> Logger { get; }
}

/// <summary>Provides the typed EMA realtime context.</summary>
public sealed class FuturesEmaSignalRealtimeContext : EventActorContext,
    IRealtimeActorContext<FuturesEmaSignalRealtimeActor>, IFuturesEmaSignalRealtimeContext
{
    /// <summary>Initializes the context.</summary>
    public FuturesEmaSignalRealtimeContext(IActorSupervisor supervisor,
        ILogger<FuturesEmaSignalRealtimeActor> logger)
        : base(supervisor, new(ActorType.Realtime, FuturesEmaSignalRealtimeActor.ActorName)) =>
        Logger = IsArgumentNull.Set(logger);
    /// <summary>Gets the actor logger.</summary>
    public ILogger<FuturesEmaSignalRealtimeActor> Logger { get; }
}
