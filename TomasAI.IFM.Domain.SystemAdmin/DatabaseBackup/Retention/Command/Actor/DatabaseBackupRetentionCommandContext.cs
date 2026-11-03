using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.Actor;

/// <summary>Provides the typed runtime context used by <see cref="DatabaseBackupRetentionCommandActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class DatabaseBackupRetentionCommandContext : CommandActorContext, IDatabaseBackupRetentionCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public DatabaseBackupRetentionCommandContext(
        IActorSupervisor supervisor,
        IEventProjector<DatabaseBackupRetentionCommandActor> eventProjector,
        ILogger<DatabaseBackupRetentionCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, DatabaseBackupRetentionCommandActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IEventProjector<DatabaseBackupRetentionCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<DatabaseBackupRetentionCommandActor> Logger { get; }
}
