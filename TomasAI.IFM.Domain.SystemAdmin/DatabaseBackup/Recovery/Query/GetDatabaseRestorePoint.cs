using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Query;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Query;

/// <summary>Handles <see cref="GetDatabaseRestorePointQuery"/>.</summary>
public static class GetDatabaseRestorePoint
{

    extension(GetDatabaseRestorePointQuery query)
    {
        /// <summary>Reads the requested Database Backup projection and sends its typed response.</summary>
        public async ValueTask ExecuteAsync(ISystemAdminDbContext dbContext, IQueryActorContext<DatabaseRecoveryQueryActor> context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(query);
            var result = await (dbContext.GetRestorePointAsync(query, cancellationToken)).ConfigureAwait(false);
            await (DatabaseBackupQueryReply.OptionalAsync(context, query, result)).ConfigureAwait(false);
        }
    }
}
