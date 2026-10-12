using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;

/// <summary>Defines readonly services required by the trade-session bar signal Realtime actor.</summary>
public interface IFuturesTradeSessionBarSignalRealtimeContext
    : IRealtimeActorContext<FuturesTradeSessionBarSignalRealtimeActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the actor-centric model that builds ephemeral open bars.</summary>
    FuturesTradeSessionBarAccumulatorRegistry Accumulators { get; }
    /// <summary>Gets the authoritative futures session calendar.</summary>
    IMarketSessionCalendar Calendar { get; }
    /// <summary>Gets the server clock.</summary>
    TimeProvider TimeProvider { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesTradeSessionBarSignalRealtimeActor> Logger { get; }
}
