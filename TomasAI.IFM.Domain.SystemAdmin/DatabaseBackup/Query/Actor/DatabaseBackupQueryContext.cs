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
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<DatabaseBackupQueryActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="DatabaseBackupQueryActor"/>.</summary>
public sealed class DatabaseBackupQueryContext : QueryActorContext, IQueryActorContext<DatabaseBackupQueryActor>, IDatabaseBackupQueryContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public DatabaseBackupQueryContext(
        IActorSupervisor supervisor,
        ISystemAdminDbContext dbContext,
        ILogger<DatabaseBackupQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, DatabaseBackupQueryActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbContext = IsArgumentNull.Set(dbContext);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ISystemAdminDbContext DbContext { get; }
    /// <inheritdoc/>
    public ILogger<DatabaseBackupQueryActor> Logger { get; }
}
