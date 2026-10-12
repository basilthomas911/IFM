using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesTdiSignalEventActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesTdiSignalEventContext : EventActorContext, IFuturesTdiSignalEventContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesTdiSignalEventContext(
        IActorSupervisor supervisor,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger<FuturesTdiSignalEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesTdiSignalEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <inheritdoc/>
    public ILogger<FuturesTdiSignalEventActor> Logger { get; }
}
