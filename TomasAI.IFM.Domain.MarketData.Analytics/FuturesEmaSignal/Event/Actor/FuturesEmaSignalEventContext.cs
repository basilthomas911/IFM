using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Event.Actor;

/// <summary>Defines the readonly EMA event context.</summary>
public interface IFuturesEmaSignalEventContext : IEventActorContext<FuturesEmaSignalEventActor>
{
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesEmaSignalEventActor> Logger { get; }
}

/// <summary>Provides the typed EMA event context.</summary>
public sealed class FuturesEmaSignalEventContext : EventActorContext,
    IEventActorContext<FuturesEmaSignalEventActor>, IFuturesEmaSignalEventContext
{
    /// <summary>Initializes the context.</summary>
    public FuturesEmaSignalEventContext(IActorSupervisor supervisor,
        ILogger<FuturesEmaSignalEventActor> logger)
        : base(supervisor, new(ActorType.Event, FuturesEmaSignalEventActor.ActorName)) =>
        Logger = IsArgumentNull.Set(logger);
    /// <summary>Gets the actor logger.</summary>
    public ILogger<FuturesEmaSignalEventActor> Logger { get; }
}
