using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Query;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Query.Actor;

/// <summary>Provides the typed runtime context used by <see cref="DatabaseBackupSetQueryActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class DatabaseBackupSetQueryContext : QueryActorContext, IDatabaseBackupSetQueryContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public DatabaseBackupSetQueryContext(
        IActorSupervisor supervisor,
        ISystemAdminDbContext dbContext,
        ILogger<DatabaseBackupSetQueryActor> logger, IDatabaseBackupOutputReader? outputReader = null)
        : base(supervisor, new ActorMailboxId(ActorType.Query, DatabaseBackupSetQueryActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbContext = IsArgumentNull.Set(dbContext);
        Logger = IsArgumentNull.Set(logger);
        OutputReader = outputReader;
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public ISystemAdminDbContext DbContext { get; }
    /// <inheritdoc/>
    public ILogger<DatabaseBackupSetQueryActor> Logger { get; }
    /// <inheritdoc />
    public IDatabaseBackupOutputReader? OutputReader { get; }
}
