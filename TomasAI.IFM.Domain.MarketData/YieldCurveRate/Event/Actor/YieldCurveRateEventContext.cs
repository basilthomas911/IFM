using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="YieldCurveRateEventActor"/>.</summary>
public sealed class YieldCurveRateEventContext : EventActorContext, IYieldCurveRateEventContext
{
    /// <summary>Initializes the context.</summary>
    public YieldCurveRateEventContext(IActorSupervisor supervisor, IMarketDataApi marketDataApi,
        IDbContextFactory dbFactory, ILogger<YieldCurveRateEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, YieldCurveRateEventActor.Actor))
    { Supervisor = IsArgumentNull.Set(supervisor); MarketDataApi = IsArgumentNull.Set(marketDataApi); DbFactory = IsArgumentNull.Set(dbFactory); Logger = IsArgumentNull.Set(logger); }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IMarketDataApi MarketDataApi { get; }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<YieldCurveRateEventActor> Logger { get; }
}
