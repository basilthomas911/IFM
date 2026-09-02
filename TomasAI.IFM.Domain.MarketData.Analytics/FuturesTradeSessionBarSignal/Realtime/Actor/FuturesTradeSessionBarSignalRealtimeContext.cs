using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;

/// <summary>Defines readonly services required by the trade-session bar signal Realtime actor.</summary>
public interface IFuturesTradeSessionBarSignalRealtimeContext
    : IRealtimeActorContext<FuturesTradeSessionBarSignalRealtimeActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the actor-centric model that builds ephemeral open bars.</summary>
    FuturesTradeSessionBarAccumulatorRegistry Accumulators { get; }
    /// <summary>Gets the authoritative futures session calendar.</summary>
    IMarketSessionCalendar Calendar { get; }
    /// <summary>Gets the server clock.</summary>
    TimeProvider TimeProvider { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesTradeSessionBarSignalRealtimeActor> Logger { get; }
}

/// <summary>Provides the closed generic context used by the trade-session bar signal Realtime actor.</summary>
public sealed class FuturesTradeSessionBarSignalRealtimeContext
    : EventActorContext,
      IRealtimeActorContext<FuturesTradeSessionBarSignalRealtimeActor>,
      IFuturesTradeSessionBarSignalRealtimeContext
{
    /// <summary>Initializes the readonly context.</summary>
    public FuturesTradeSessionBarSignalRealtimeContext(
        IActorSupervisor supervisor,
        FuturesTradeSessionBarAccumulatorRegistry accumulators,
        IMarketSessionCalendar calendar,
        TimeProvider timeProvider,
        ILogger<FuturesTradeSessionBarSignalRealtimeActor> logger)
        : base(supervisor, new(ActorType.Realtime, FuturesTradeSessionBarSignalRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Accumulators = IsArgumentNull.Set(accumulators);
        Calendar = IsArgumentNull.Set(calendar);
        TimeProvider = IsArgumentNull.Set(timeProvider);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc />
    public FuturesTradeSessionBarAccumulatorRegistry Accumulators { get; }
    /// <inheritdoc />
    public IMarketSessionCalendar Calendar { get; }
    /// <inheritdoc />
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc />
    public ILogger<FuturesTradeSessionBarSignalRealtimeActor> Logger { get; }
}
