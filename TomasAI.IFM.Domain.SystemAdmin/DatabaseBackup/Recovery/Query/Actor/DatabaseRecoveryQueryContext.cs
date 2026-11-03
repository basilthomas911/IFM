using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Query;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Query.Actor;

/// <summary>Provides the typed runtime context used by <see cref="DatabaseRecoveryQueryActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class DatabaseRecoveryQueryContext : QueryActorContext, IDatabaseRecoveryQueryContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public DatabaseRecoveryQueryContext(
        IActorSupervisor supervisor,
        ISystemAdminDbContext dbContext,
        ILogger<DatabaseRecoveryQueryActor> logger, IDatabaseBackupOutputReader? outputReader = null)
        : base(supervisor, new ActorMailboxId(ActorType.Query, DatabaseRecoveryQueryActor.Actor))
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
    public ILogger<DatabaseRecoveryQueryActor> Logger { get; }
    /// <inheritdoc />
    public IDatabaseBackupOutputReader? OutputReader { get; }
}
