using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Query;

/// <summary>Handles <see cref="GetDatabaseBackupServiceHealthQuery"/>.</summary>
public static class GetDatabaseBackupServiceHealth
{

    extension(GetDatabaseBackupServiceHealthQuery query)
    {
        /// <summary>Reads the requested Database Backup projection and returns its typed result.</summary>
        public async ValueTask<ServiceResult<DatabaseBackupHealthReadModel[]>> ExecuteAsync(ISystemAdminDbReadContext dbContext, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(query);
            var result = await (dbContext.GetServiceHealthAsync(query, cancellationToken)).ConfigureAwait(false);
            return new ServiceOk<DatabaseBackupHealthReadModel[]>(result);
        }
    }
}
