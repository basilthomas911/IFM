using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.MarketData.OperationsHealth;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesItiSignalCommandActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesItiSignalCommandContext : CommandActorContext, IFuturesItiSignalCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesItiSignalCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IEventProjector<FuturesItiSignalCommandActor> eventProjector,
        ILogger<FuturesItiSignalCommandActor> logger,
        FuturesItiSignalRuntimeTelemetry? telemetry = null)
        : base(supervisor, new ActorMailboxId(ActorType.Command, FuturesItiSignalCommandActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbEventSource = IsArgumentNull.Set(dbEventSource);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
        Telemetry = telemetry ?? new FuturesItiSignalRuntimeTelemetry(TimeProvider.System);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource { get; }
    /// <inheritdoc/>
    public IEventProjector<FuturesItiSignalCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<FuturesItiSignalCommandActor> Logger { get; }
    /// <inheritdoc/>
    public FuturesItiSignalRuntimeTelemetry Telemetry { get; }
}
