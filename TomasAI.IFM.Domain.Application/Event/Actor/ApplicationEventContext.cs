using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.Application.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="ApplicationEventActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class ApplicationEventContext : EventActorContext, IApplicationEventContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public ApplicationEventContext(
        IActorSupervisor supervisor,
        IApplicationStartupActivities startupActivities,
        IApplicationStartupStatusStore startupStatusStore,
        IStatusConsoleWriter statusConsoleWriter,
        TimeProvider timeProvider,
        ILogger<ApplicationEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, ApplicationEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        StartupActivities = IsArgumentNull.Set(startupActivities);
        StartupStatusStore = IsArgumentNull.Set(startupStatusStore);
        StatusConsoleWriter = IsArgumentNull.Set(statusConsoleWriter);
        TimeProvider = IsArgumentNull.Set(timeProvider);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IApplicationStartupActivities StartupActivities { get; }
    /// <inheritdoc/>
    public IApplicationStartupStatusStore StartupStatusStore { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <inheritdoc/>
    public TimeProvider TimeProvider { get; }
    /// <inheritdoc/>
    public ILogger<ApplicationEventActor> Logger { get; }
}
