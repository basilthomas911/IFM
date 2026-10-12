using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Realtime.Actor;

/// <summary>Provides the typed VX term-structure Realtime context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesVxTermStructureSignalRealtimeContext : EventActorContext,
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
