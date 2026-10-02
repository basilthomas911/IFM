using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesEodDataRealtimeActor"/>.</summary>
public interface IFuturesEodDataRealtimeContext : IRealtimeActorContext<FuturesEodDataRealtimeActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesEodDataRealtimeActor> Logger { get; }
    /// <summary>Gets the Projector service.</summary>
    IRealtimeProjector<FuturesEodDataRealtimeActor> Projector { get; }
    /// <summary>Gets the MarketDataApi service.</summary>
    IMarketDataApi MarketDataApi { get; }
    /// <summary>Gets the BlackboardService service.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the StatusConsoleWriter service.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <summary>Enables the bounded, process-local trade handoff for basic latency testing.</summary>
    bool EnableAsyncTradeWorker { get; }
    /// <summary>Gets the per-contract worker and trade-cache state.</summary>
    FuturesEodTradeDispatchState TradeDispatch { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesEodDataRealtimeActor"/>.</summary>
public sealed class FuturesEodDataRealtimeContext : EventActorContext, IRealtimeActorContext<FuturesEodDataRealtimeActor>, IFuturesEodDataRealtimeContext
{
    /// <summary>Initializes the typed realtime context.</summary>
    public FuturesEodDataRealtimeContext(
        IActorSupervisor supervisor,
        ILogger<FuturesEodDataRealtimeActor> logger,
        IRealtimeProjector<FuturesEodDataRealtimeActor> projector,
        IMarketDataApi marketDataApi,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter,
        IConfiguration? configuration = null)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesEodDataRealtimeActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
        Projector = IsArgumentNull.Set(projector);
        MarketDataApi = IsArgumentNull.Set(marketDataApi);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
        EnableAsyncTradeWorker = bool.TryParse(
            configuration?["MarketData:FuturesEodAsyncTradeWorker"], out var enabled) && enabled;
    }
    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<FuturesEodDataRealtimeActor> Logger { get; }
    /// <inheritdoc/>
    public IRealtimeProjector<FuturesEodDataRealtimeActor> Projector { get; }
    /// <inheritdoc/>
    public IMarketDataApi MarketDataApi { get; }
    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <inheritdoc/>
    public bool EnableAsyncTradeWorker { get; }
    /// <inheritdoc/>
    public FuturesEodTradeDispatchState TradeDispatch { get; } = new();
}

