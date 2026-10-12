using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;

/// <summary>Provides the closed generic context used by the trade-session bar signal Realtime actor.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesTradeSessionBarSignalRealtimeContext
    : EventActorContext,
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
