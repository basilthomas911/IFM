using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Command;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Command.Actor;

/// <summary>Provides the typed runtime context used by <see cref="DatabaseBackupServiceCommandActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class DatabaseBackupServiceCommandContext : CommandActorContext, IDatabaseBackupServiceCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public DatabaseBackupServiceCommandContext(
        IActorSupervisor supervisor,
        IEventProjector<DatabaseBackupServiceCommandActor> eventProjector,
        ILogger<DatabaseBackupServiceCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, DatabaseBackupServiceCommandActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IEventProjector<DatabaseBackupServiceCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<DatabaseBackupServiceCommandActor> Logger { get; }
}
