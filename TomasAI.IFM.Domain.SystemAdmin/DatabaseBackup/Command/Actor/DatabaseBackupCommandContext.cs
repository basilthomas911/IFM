using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Actor;

/// <summary>Provides the typed runtime context used by <see cref="DatabaseBackupCommandActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class DatabaseBackupCommandContext : CommandActorContext, IDatabaseBackupCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public DatabaseBackupCommandContext(
        IActorSupervisor supervisor,
        IEventProjector<DatabaseBackupCommandActor> eventProjector,
        ILogger<DatabaseBackupCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, DatabaseBackupCommandActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IEventProjector<DatabaseBackupCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<DatabaseBackupCommandActor> Logger { get; }
}
