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

    /// <summary>Gets the single-operation generation gate. The gate never retains skipped ticks.</summary>
    FuturesItiSignalGenerationGate GenerationGate { get; }

    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesItiSignalRealtimeActor> Logger { get; }
}

/// <summary>Provides the typed runtime context for the Futures ITI realtime ingress actor.</summary>
public sealed class FuturesItiSignalRealtimeContext : EventActorContext,
    IRealtimeActorContext<FuturesItiSignalRealtimeActor>, IFuturesItiSignalRealtimeContext
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
        GenerationGate = new FuturesItiSignalGenerationGate();
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public IMarketDataApi MarketDataApi { get; }

    /// <inheritdoc />
    public LivePipelineEvidence HealthEvidence { get; }

    /// <inheritdoc />
    public FuturesItiSignalRuntimeTelemetry Telemetry { get; }

    /// <inheritdoc />
    public FuturesItiSignalGenerationGate GenerationGate { get; }

    /// <inheritdoc />
    public ILogger<FuturesItiSignalRealtimeActor> Logger { get; }
}
