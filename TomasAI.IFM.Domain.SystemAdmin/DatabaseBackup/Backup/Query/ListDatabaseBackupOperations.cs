using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Query;

/// <summary>Handles <see cref="ListDatabaseBackupOperationsQuery"/>.</summary>
public static class ListDatabaseBackupOperations
{

    extension(ListDatabaseBackupOperationsQuery query)
    {
        /// <summary>Reads the requested Database Backup projection and returns its typed result.</summary>
        public async ValueTask<ServiceResult<DatabaseBackupOperationReadModel[]>> ExecuteAsync(ISystemAdminDbReadContext dbContext, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(query);
            var result = await (dbContext.ListBackupOperationsAsync(query, cancellationToken)).ConfigureAwait(false);
            return new ServiceOk<DatabaseBackupOperationReadModel[]>(result);
        }
    }
}
