using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.Application.Actor.Event.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="ApplicationEventActor"/>.</summary>
public interface IApplicationEventContext : IEventActorContext<ApplicationEventActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<ApplicationEventActor> Logger { get; }
    /// <summary>Gets the ordered startup activity adapter.</summary>
    IApplicationStartupActivities StartupActivities { get; }
    /// <summary>Gets the latest lifecycle status store.</summary>
    IApplicationStartupStatusStore StartupStatusStore { get; }
    /// <summary>Gets the unattended system-console writer.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <summary>Gets the clock used for deterministic lifecycle timestamps.</summary>
    TimeProvider TimeProvider { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="ApplicationEventActor"/>.</summary>
public sealed class ApplicationEventContext : EventActorContext, IEventActorContext<ApplicationEventActor>, IApplicationEventContext
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
