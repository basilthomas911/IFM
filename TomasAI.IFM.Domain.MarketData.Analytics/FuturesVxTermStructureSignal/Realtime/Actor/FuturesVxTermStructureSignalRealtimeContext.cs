using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Realtime.Actor;

/// <summary>Defines readonly services required by the VX term-structure Realtime actor.</summary>
public interface IFuturesVxTermStructureSignalRealtimeContext
    : IRealtimeActorContext<FuturesVxTermStructureSignalRealtimeActor>
{
    IMarketDataApi MarketDataApi { get; }
    ILogger<FuturesVxTermStructureSignalRealtimeActor> Logger { get; }
}

/// <summary>Provides the typed VX term-structure Realtime context.</summary>
public sealed class FuturesVxTermStructureSignalRealtimeContext : EventActorContext,
    IRealtimeActorContext<FuturesVxTermStructureSignalRealtimeActor>,
    IFuturesVxTermStructureSignalRealtimeContext
{
    /// <summary>Initializes the typed context.</summary>
    public FuturesVxTermStructureSignalRealtimeContext(
        IActorSupervisor supervisor,
        IMarketDataApi marketDataApi,
        ILogger<FuturesVxTermStructureSignalRealtimeActor> logger)
        : base(supervisor, new(ActorType.Realtime, FuturesVxTermStructureSignalRealtimeActor.ActorName))
    {
        MarketDataApi = IsArgumentNull.Set(marketDataApi);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc />
    public IMarketDataApi MarketDataApi { get; }
    /// <inheritdoc />
    public ILogger<FuturesVxTermStructureSignalRealtimeActor> Logger { get; }
}
