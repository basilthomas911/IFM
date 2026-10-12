using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;

/// <summary>Provides the typed runtime context for the Futures ITI realtime ingress actor.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesItiSignalRealtimeContext : EventActorContext,
IFuturesItiSignalRealtimeContext
{
    /// <summary>Initializes the minimal typed realtime context.</summary>
    public FuturesItiSignalRealtimeContext(
        IActorSupervisor supervisor,
        IMarketDataApi marketDataApi,
        LivePipelineEvidence healthEvidence,
        FuturesItiSignalRuntimeTelemetry telemetry,
        ILogger<FuturesItiSignalRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesItiSignalRealtimeActor.ActorName))
    {
        MarketDataApi = IsArgumentNull.Set(marketDataApi);
        HealthEvidence = IsArgumentNull.Set(healthEvidence);
        Telemetry = IsArgumentNull.Set(telemetry);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public IMarketDataApi MarketDataApi { get; }

    /// <inheritdoc />
    public LivePipelineEvidence HealthEvidence { get; }

    /// <inheritdoc />
    public FuturesItiSignalRuntimeTelemetry Telemetry { get; }

    /// <inheritdoc />
    public ILogger<FuturesItiSignalRealtimeActor> Logger { get; }
}
