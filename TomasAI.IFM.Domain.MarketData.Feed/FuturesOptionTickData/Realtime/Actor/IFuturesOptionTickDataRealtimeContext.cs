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

