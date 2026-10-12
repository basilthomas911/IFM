using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="DatabaseBackupQueryActor"/>.</summary>
public interface IDatabaseBackupQueryContext : IQueryActorContext<DatabaseBackupQueryActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbContext service supplied to the actor context.</summary>
    ISystemAdminDbContext DbContext { get; }
    /// <summary>Gets the retained operation output reader.</summary>
    IDatabaseBackupOutputReader? OutputReader { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<DatabaseBackupQueryActor> Logger { get; }
}
