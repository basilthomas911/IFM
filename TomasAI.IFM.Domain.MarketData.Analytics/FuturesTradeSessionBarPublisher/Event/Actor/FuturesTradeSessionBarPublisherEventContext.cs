using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Event.Actor;

/// <summary>Defines readonly services required by the stateless bar publisher Event actor.</summary>
public interface IFuturesTradeSessionBarPublisherEventContext
    : IEventActorContext<FuturesTradeSessionBarPublisherEventActor>
{
    /// <summary>Gets the server clock used to timestamp realtime publication.</summary>
    TimeProvider TimeProvider { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesTradeSessionBarPublisherEventActor> Logger { get; }
}

/// <summary>Provides the closed generic context for the bar publisher Event actor.</summary>
public sealed class FuturesTradeSessionBarPublisherEventContext
    : EventActorContext,
      IEventActorContext<FuturesTradeSessionBarPublisherEventActor>,
      IFuturesTradeSessionBarPublisherEventContext
{
    /// <summary>Initializes the immutable Event context.</summary>
    public FuturesTradeSessionBarPublisherEventContext(
        IActorSupervisor supervisor,
        TimeProvider timeProvider,
        ILogger<FuturesTradeSessionBarPublisherEventActor> logger)
        : base(supervisor, new(ActorType.Event, FuturesTradeSessionBarPublisherEventActor.ActorName))
    {
        TimeProvider = IsArgumentNull.Set(timeProvider);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc />
    public ILogger<FuturesTradeSessionBarPublisherEventActor> Logger { get; }
}
