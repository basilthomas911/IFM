using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Query;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Query;

/// <summary>Handles <see cref="GetDatabaseBackupSetQuery"/>.</summary>
public static class GetDatabaseBackupSet
{

    extension(GetDatabaseBackupSetQuery query)
    {
        /// <summary>Reads the requested Database Backup projection and returns its typed result.</summary>
        public async ValueTask<ServiceResult<DatabaseBackupSetReadModel>> ExecuteAsync(ISystemAdminDbReadContext dbContext, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(query);
            var operations = await dbContext.GetBackupSetOperationsAsync(query.BackupSetId!.Value.Value, cancellationToken).ConfigureAwait(false);
            var result = BuildReadModel(query, operations);
            return DatabaseBackupQueryResult.Optional(result);
        }
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
