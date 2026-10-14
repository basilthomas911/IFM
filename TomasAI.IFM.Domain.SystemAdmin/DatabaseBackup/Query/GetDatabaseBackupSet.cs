using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Model;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query;

/// <summary>Handles <see cref="GetDatabaseBackupSetQuery"/>.</summary>
public static class GetDatabaseBackupSet
{
    /// <summary>Reads the requested Database Backup projection and sends its typed response.</summary>
    public static async ValueTask ExecuteAsync(this GetDatabaseBackupSetQuery query, ISystemAdminDbContext dbContext, IQueryActorContext<DatabaseBackupQueryActor> context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(query);
        var operations = await dbContext.GetBackupSetOperationsAsync(query.BackupSetId!.Value.Value, cancellationToken).ConfigureAwait(false);
        var result = BuildReadModel(query, operations);
        await (DatabaseBackupQueryReply.OptionalAsync(context, query, result)).ConfigureAwait(false);
    }
    /// <summary>Builds backup-set completeness from persisted operation facts.</summary>
    internal static DatabaseBackupSetReadModel? BuildReadModel(GetDatabaseBackupSetQuery query, DatabaseBackupOperationReadModel[] operations)
    {
        if (operations.Length == 0) return null;
        return new DatabaseBackupSetReadModel
        {
            BackupSetId = query.BackupSetId.Value,
            Source = operations[0].Source,
            OperationIds = [.. operations.Select(static operation => operation.OperationId)],
            RequiredOperationCount = operations.Length,
            CompletedOperationCount = operations.Count(static operation => operation.Outcome == DatabaseRecoveryOutcome.Succeeded),
            Complete = operations.All(static operation => operation.Outcome == DatabaseRecoveryOutcome.Succeeded),
            Revision = operations.Max(static operation => operation.StateRevision)
        };
    }
}
