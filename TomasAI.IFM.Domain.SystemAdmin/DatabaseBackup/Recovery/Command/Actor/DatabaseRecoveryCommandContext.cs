using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.Actor;

/// <summary>Provides the typed runtime context used by <see cref="DatabaseRecoveryCommandActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class DatabaseRecoveryCommandContext : CommandActorContext, IDatabaseRecoveryCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public DatabaseRecoveryCommandContext(
        IActorSupervisor supervisor,
        IEventProjector<DatabaseRecoveryCommandActor> eventProjector,
        ILogger<DatabaseRecoveryCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, DatabaseRecoveryCommandActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IEventProjector<DatabaseRecoveryCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<DatabaseRecoveryCommandActor> Logger { get; }
}
