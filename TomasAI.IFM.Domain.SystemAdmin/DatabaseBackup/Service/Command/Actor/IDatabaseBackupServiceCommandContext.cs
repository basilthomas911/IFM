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

/// <summary>Defines the readonly runtime services required by <see cref="DatabaseBackupServiceCommandActor"/>.</summary>
public interface IDatabaseBackupServiceCommandContext : ICommandActorContext<DatabaseBackupServiceCommandActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the EventProjector service supplied to the actor context.</summary>
    IEventProjector<DatabaseBackupServiceCommandActor> EventProjector { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<DatabaseBackupServiceCommandActor> Logger { get; }
}
