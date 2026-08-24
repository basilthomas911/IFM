using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesItiSignalRealtimeActor"/>.</summary>
public interface IFuturesItiSignalRealtimeContext : IRealtimeActorContext<FuturesItiSignalRealtimeActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the Projector service supplied to the actor context.</summary>
    IRealtimeProjector<FuturesItiSignalRealtimeActor> Projector { get; }
    /// <summary>Gets the MarketDataApi service supplied to the actor context.</summary>
    IMarketDataApi MarketDataApi { get; }
    /// <summary>Gets the DbFactory service supplied to the actor context.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the StatusConsoleWriter service supplied to the actor context.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesItiSignalRealtimeActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesItiSignalRealtimeActor"/>.</summary>
public sealed class FuturesItiSignalRealtimeContext : EventActorContext, IRealtimeActorContext<FuturesItiSignalRealtimeActor>, IFuturesItiSignalRealtimeContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesItiSignalRealtimeContext(
        IActorSupervisor supervisor,
        IRealtimeProjector<FuturesItiSignalRealtimeActor> projector,
        IMarketDataApi marketDataApi,
        IDbContextFactory dbFactory,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesItiSignalRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesItiSignalRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Projector = IsArgumentNull.Set(projector);
        MarketDataApi = IsArgumentNull.Set(marketDataApi);
        DbFactory = IsArgumentNull.Set(dbFactory);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IRealtimeProjector<FuturesItiSignalRealtimeActor> Projector { get; }
    /// <inheritdoc/>
    public IMarketDataApi MarketDataApi { get; }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <inheritdoc/>
    public ILogger<FuturesItiSignalRealtimeActor> Logger { get; }
}
