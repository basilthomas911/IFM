using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesItiSignalEventActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesItiSignalEventContext : EventActorContext, IFuturesItiSignalEventContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesItiSignalEventContext(
        IActorSupervisor supervisor,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesItiSignalEventActor> logger,
        IIntrinsicTimeWorkflowStartPolicy workflowStartPolicy,
        FuturesItiSignalRuntimeTelemetry? telemetry = null)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesItiSignalEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
        Logger = IsArgumentNull.Set(logger);
        WorkflowStartPolicy = IsArgumentNull.Set(workflowStartPolicy);
        Telemetry = telemetry ?? new FuturesItiSignalRuntimeTelemetry(TimeProvider.System);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <inheritdoc/>
    public ILogger<FuturesItiSignalEventActor> Logger { get; }
    /// <inheritdoc/>
    public FuturesItiSignalRuntimeTelemetry Telemetry { get; }
    /// <inheritdoc/>
    public IIntrinsicTimeWorkflowStartPolicy WorkflowStartPolicy { get; }
}
