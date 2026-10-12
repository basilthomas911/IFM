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

