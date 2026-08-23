using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Realtime.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesOptionTickDataRealtimeActor"/>.</summary>
public interface IFuturesOptionTickDataRealtimeContext : IRealtimeActorContext<FuturesOptionTickDataRealtimeActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesOptionTickDataRealtimeActor> Logger { get; }
    /// <summary>Gets the MarketDataApi service.</summary>
    IMarketDataApi MarketDataApi { get; }
    /// <summary>Gets the StatusConsoleWriter service.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesOptionTickDataRealtimeActor"/>.</summary>
public sealed class FuturesOptionTickDataRealtimeContext : EventActorContext, IRealtimeActorContext<FuturesOptionTickDataRealtimeActor>, IFuturesOptionTickDataRealtimeContext
{
    /// <summary>Initializes the typed realtime context.</summary>
    public FuturesOptionTickDataRealtimeContext(
        IActorSupervisor supervisor,
        ILogger<FuturesOptionTickDataRealtimeActor> logger,
        IMarketDataApi marketDataApi,
        IStatusConsoleWriter statusConsoleWriter)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesOptionTickDataRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
        MarketDataApi = IsArgumentNull.Set(marketDataApi);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
    }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<FuturesOptionTickDataRealtimeActor> Logger { get; }
    /// <inheritdoc/>
    public IMarketDataApi MarketDataApi { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
}

