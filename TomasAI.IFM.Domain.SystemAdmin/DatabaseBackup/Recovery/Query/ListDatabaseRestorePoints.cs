using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Query;

/// <summary>Handles <see cref="ListDatabaseRestorePointsQuery"/>.</summary>
public static class ListDatabaseRestorePoints
{

    extension(ListDatabaseRestorePointsQuery query)
    {
        /// <summary>Reads the requested Database Backup projection and returns its typed result.</summary>
        public async ValueTask<ServiceResult<DatabaseRestorePointReadModel[]>> ExecuteAsync(ISystemAdminDbReadContext dbContext, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(query);
            var result = await (dbContext.ListRestorePointsAsync(query, cancellationToken)).ConfigureAwait(false);
            return new ServiceOk<DatabaseRestorePointReadModel[]>(result);
        }
    }
}
