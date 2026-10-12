using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Application.Command;
using TomasAI.IFM.Domain.Application.Command.State;
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

namespace TomasAI.IFM.Domain.Application.Command.Actor;

/// <summary>Provides the typed runtime context used by <see cref="ApplicationCommandActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class ApplicationCommandContext : CommandActorContext, IApplicationCommandContext
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
