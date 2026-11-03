using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Event;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Event.Actor;

/// <summary>Provides the typed runtime context used by <see cref="DatabaseRecoveryEventActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class DatabaseRecoveryEventContext : EventActorContext, IDatabaseRecoveryEventContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public DatabaseRecoveryEventContext(
        IActorSupervisor supervisor,
        ILogger<DatabaseRecoveryEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, DatabaseRecoveryEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ILogger<DatabaseRecoveryEventActor> Logger { get; }
}
