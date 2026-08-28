using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event.Actor;

/// <summary>Defines readonly services required by the stateless bar signal Event actor.</summary>
public interface IFuturesTradeSessionBarSignalEventContext
    : IEventActorContext<FuturesTradeSessionBarSignalEventActor>
{
    /// <summary>Gets the server clock used to timestamp realtime publication.</summary>
    TimeProvider TimeProvider { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesTradeSessionBarSignalEventActor> Logger { get; }
}

/// <summary>Provides the closed generic context for the bar signal Event actor.</summary>
public sealed class FuturesTradeSessionBarSignalEventContext
    : EventActorContext,
      IEventActorContext<FuturesTradeSessionBarSignalEventActor>,
      IFuturesTradeSessionBarSignalEventContext
{
    /// <summary>Initializes the immutable Event context.</summary>
    public FuturesTradeSessionBarSignalEventContext(
        IActorSupervisor supervisor,
        TimeProvider timeProvider,
        ILogger<FuturesTradeSessionBarSignalEventActor> logger)
        : base(supervisor, new(ActorType.Event, FuturesTradeSessionBarSignalEventActor.ActorName))
    {
        TimeProvider = IsArgumentNull.Set(timeProvider);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc />
    public ILogger<FuturesTradeSessionBarSignalEventActor> Logger { get; }
}
