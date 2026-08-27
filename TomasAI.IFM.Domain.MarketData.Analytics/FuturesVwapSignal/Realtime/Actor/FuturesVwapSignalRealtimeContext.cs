using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Actor;

/// <summary>Defines readonly services required by the stateless VWAP Realtime actor.</summary>
public interface IFuturesVwapSignalRealtimeContext
    : IRealtimeActorContext<FuturesVwapSignalRealtimeActor>
{
    IMarketDataApi MarketDataApi { get; }
    IMarketSessionCalendar SessionCalendar { get; }
    ILogger<FuturesVwapSignalRealtimeActor> Logger { get; }
}

/// <summary>Provides the closed generic VWAP Realtime context.</summary>
public sealed class FuturesVwapSignalRealtimeContext : EventActorContext,
    IRealtimeActorContext<FuturesVwapSignalRealtimeActor>, IFuturesVwapSignalRealtimeContext
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
