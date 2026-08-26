using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Realtime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Realtime.Actor;

/// <summary>Defines readonly services required by the trade-session bar publisher Realtime actor.</summary>
public interface IFuturesTradeSessionBarPublisherRealtimeContext
    : IRealtimeActorContext<FuturesTradeSessionBarPublisherRealtimeActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the actor-centric model that builds ephemeral open bars.</summary>
    FuturesTradeSessionBarAccumulator Accumulator { get; }
    /// <summary>Gets the server clock.</summary>
    TimeProvider TimeProvider { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesTradeSessionBarPublisherRealtimeActor> Logger { get; }
}

/// <summary>Provides the closed generic context used by the trade-session bar publisher Realtime actor.</summary>
public sealed class FuturesTradeSessionBarPublisherRealtimeContext
    : EventActorContext,
      IRealtimeActorContext<FuturesTradeSessionBarPublisherRealtimeActor>,
      IFuturesTradeSessionBarPublisherRealtimeContext
{
    /// <summary>Initializes the readonly context.</summary>
    public FuturesTradeSessionBarPublisherRealtimeContext(
        IActorSupervisor supervisor,
        FuturesTradeSessionBarAccumulator accumulator,
        TimeProvider timeProvider,
        ILogger<FuturesTradeSessionBarPublisherRealtimeActor> logger)
        : base(supervisor, new(ActorType.Realtime, FuturesTradeSessionBarPublisherRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Accumulator = IsArgumentNull.Set(accumulator);
        TimeProvider = IsArgumentNull.Set(timeProvider);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc />
    public FuturesTradeSessionBarAccumulator Accumulator { get; }
    /// <inheritdoc />
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc />
    public ILogger<FuturesTradeSessionBarPublisherRealtimeActor> Logger { get; }
}
