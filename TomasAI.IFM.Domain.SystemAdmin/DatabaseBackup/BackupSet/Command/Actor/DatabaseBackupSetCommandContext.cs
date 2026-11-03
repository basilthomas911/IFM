using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.Actor;

/// <summary>Provides the typed runtime context used by <see cref="DatabaseBackupSetCommandActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class DatabaseBackupSetCommandContext : CommandActorContext, IDatabaseBackupSetCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public DatabaseBackupSetCommandContext(
        IActorSupervisor supervisor,
        IEventProjector<DatabaseBackupSetCommandActor> eventProjector,
        ILogger<DatabaseBackupSetCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, DatabaseBackupSetCommandActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IEventProjector<DatabaseBackupSetCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<DatabaseBackupSetCommandActor> Logger { get; }
}
