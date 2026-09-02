using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Application.Actor.Command;
using TomasAI.IFM.Domain.Application.Actor.Command.State;
using TomasAI.IFM.Domain.Application.Shared.Commands;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Application.Actor.Command.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="ApplicationCommandActor"/>.</summary>
public interface IApplicationCommandContext : ICommandActorContext<ApplicationCommandActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbEventSource service supplied to the actor context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the EventProjector service supplied to the actor context.</summary>
    IEventProjector<ApplicationCommandActor> EventProjector { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<ApplicationCommandActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="ApplicationCommandActor"/>.</summary>
public sealed class ApplicationCommandContext : CommandActorContext, ICommandActorContext<ApplicationCommandActor>, IApplicationCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public ApplicationCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IEventProjector<ApplicationCommandActor> eventProjector,
        ILogger<ApplicationCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, ApplicationCommandActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbEventSource = IsArgumentNull.Set(dbEventSource);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource { get; }
    /// <inheritdoc/>
    public IEventProjector<ApplicationCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<ApplicationCommandActor> Logger { get; }
}
