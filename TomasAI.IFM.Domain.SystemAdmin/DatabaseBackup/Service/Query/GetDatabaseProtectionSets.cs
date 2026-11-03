using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Query;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Query;

/// <summary>Handles <see cref="GetDatabaseProtectionSetsQuery"/>.</summary>
public static class GetDatabaseProtectionSets
{

    extension(GetDatabaseProtectionSetsQuery query)
    {
        /// <summary>Reads the requested Database Backup projection and sends its typed response.</summary>
        public async ValueTask ExecuteAsync(ISystemAdminDbContext dbContext, IQueryActorContext<DatabaseBackupServiceQueryActor> context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(query);
            var result = await (dbContext.GetProtectionSetsAsync(query, cancellationToken)).ConfigureAwait(false);
            await (DatabaseBackupQueryReply.RequiredAsync(context, query, result)).ConfigureAwait(false);
        }
    }
}
