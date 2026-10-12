using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Actor;

/// <summary>Provides the closed generic VWAP Realtime context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesVwapSignalRealtimeContext : EventActorContext,
IFuturesVwapSignalRealtimeContext
{
    /// <summary>Initializes the readonly Realtime context.</summary>
    public FuturesVwapSignalRealtimeContext(
        IActorSupervisor supervisor,
        IMarketDataApi marketDataApi,
        IMarketSessionCalendar sessionCalendar,
        ILogger<FuturesVwapSignalRealtimeActor> logger)
        : base(supervisor, new(ActorType.Realtime, FuturesVwapSignalRealtimeActor.ActorName))
    {
        MarketDataApi = IsArgumentNull.Set(marketDataApi);
        SessionCalendar = IsArgumentNull.Set(sessionCalendar);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc />
    public IMarketDataApi MarketDataApi { get; }
    /// <inheritdoc />
    public IMarketSessionCalendar SessionCalendar { get; }
    /// <inheritdoc />
    public ILogger<FuturesVwapSignalRealtimeActor> Logger { get; }
}
