using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Event;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Event.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="DatabaseBackupRetentionEventActor"/>.</summary>
public interface IDatabaseBackupRetentionEventContext : IEventActorContext<DatabaseBackupRetentionEventActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<DatabaseBackupRetentionEventActor> Logger { get; }
}
