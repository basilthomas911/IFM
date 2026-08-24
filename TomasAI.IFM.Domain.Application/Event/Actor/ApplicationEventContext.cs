using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Application.Actor.Event.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="ApplicationEventActor"/>.</summary>
public interface IApplicationEventContext : IEventActorContext<ApplicationEventActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<ApplicationEventActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="ApplicationEventActor"/>.</summary>
public sealed class ApplicationEventContext : EventActorContext, IEventActorContext<ApplicationEventActor>, IApplicationEventContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public ApplicationEventContext(
        IActorSupervisor supervisor,
        ILogger<ApplicationEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, ApplicationEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<ApplicationEventActor> Logger { get; }
}
