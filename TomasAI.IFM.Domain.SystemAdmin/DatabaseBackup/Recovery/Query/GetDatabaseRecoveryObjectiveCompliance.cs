using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Query;

/// <summary>Handles <see cref="GetDatabaseRecoveryObjectiveComplianceQuery"/>.</summary>
public static class GetDatabaseRecoveryObjectiveCompliance
{

    extension(GetDatabaseRecoveryObjectiveComplianceQuery query)
    {
        /// <summary>Reads the requested Database Backup projection and returns its typed result.</summary>
        public async ValueTask<ServiceResult<DatabaseProtectionSetReadModel[]>> ExecuteAsync(ISystemAdminDbReadContext dbContext, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(query);
            var result = await (dbContext.GetProtectionSetsAsync(new GetDatabaseProtectionSetsQuery
            {
                EntityId = query.EntityId,
                Request = query.Request,
                Source = query.Source,
                Subject = query.Subject,
                PageSize = query.PageSize
            }, cancellationToken)).ConfigureAwait(false);
            return new ServiceOk<DatabaseProtectionSetReadModel[]>(result);
        }
    }
}
