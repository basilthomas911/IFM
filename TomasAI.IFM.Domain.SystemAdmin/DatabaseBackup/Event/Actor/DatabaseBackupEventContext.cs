using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Translation;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="DatabaseBackupEventActor"/>.</summary>
public interface IDatabaseBackupEventContext : IEventActorContext<DatabaseBackupEventActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<DatabaseBackupEventActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="DatabaseBackupEventActor"/>.</summary>
public sealed class DatabaseBackupEventContext : EventActorContext, IEventActorContext<DatabaseBackupEventActor>, IDatabaseBackupEventContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public DatabaseBackupEventContext(
        IActorSupervisor supervisor,
        ILogger<DatabaseBackupEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, DatabaseBackupEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<DatabaseBackupEventActor> Logger { get; }
}
