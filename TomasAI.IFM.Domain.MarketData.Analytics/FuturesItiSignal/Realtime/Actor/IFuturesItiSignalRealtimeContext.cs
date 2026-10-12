using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;

/// <summary>Defines the minimal services required by the Futures ITI realtime ingress actor.</summary>
public interface IFuturesItiSignalRealtimeContext : IRealtimeActorContext<FuturesItiSignalRealtimeActor>
{
    /// <summary>Gets current provider-neutral market-data state.</summary>
    IMarketDataApi MarketDataApi { get; }

    /// <summary>Gets bounded process-local pipeline evidence.</summary>
    LivePipelineEvidence HealthEvidence { get; }

    /// <summary>Gets fixed-cardinality Futures ITI telemetry.</summary>
    FuturesItiSignalRuntimeTelemetry Telemetry { get; }

    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesItiSignalRealtimeActor> Logger { get; }
}
