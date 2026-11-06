using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Query;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Query;

/// <summary>Handles <see cref="GetLatestVerifiedDatabaseBackupQuery"/>.</summary>
public static class GetLatestVerifiedDatabaseBackup
{

    extension(GetLatestVerifiedDatabaseBackupQuery query)
    {
        /// <summary>Reads the requested Database Backup projection and returns its typed result.</summary>
        public async ValueTask<ServiceResult<DatabaseRestorePointReadModel>> ExecuteAsync(ISystemAdminDbReadContext dbContext, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(query);
            var result = await (dbContext.GetLatestVerifiedBackupAsync(query, cancellationToken)).ConfigureAwait(false);
            return DatabaseBackupQueryResult.Optional(result);
        }
    }
}
