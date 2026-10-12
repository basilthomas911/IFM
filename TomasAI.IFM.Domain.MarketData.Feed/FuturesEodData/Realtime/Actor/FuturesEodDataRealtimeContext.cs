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

/// <summary>Provides the typed runtime context used by <see cref="FuturesEodDataRealtimeActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesEodDataRealtimeContext : EventActorContext, IFuturesEodDataRealtimeContext
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

