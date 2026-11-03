using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Query;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Query;

/// <summary>Handles <see cref="GetDatabaseRetentionForecastQuery"/>.</summary>
public static class GetDatabaseRetentionForecast
{

    extension(GetDatabaseRetentionForecastQuery query)
    {
        /// <summary>Reads the requested Database Backup projection and sends its typed response.</summary>
        public async ValueTask ExecuteAsync(ISystemAdminDbContext dbContext, IQueryActorContext<DatabaseBackupRetentionQueryActor> context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(query);
            var result = await (dbContext.GetRetentionForecastAsync(query, cancellationToken)).ConfigureAwait(false);
            await (DatabaseBackupQueryReply.OptionalAsync(context, query, result)).ConfigureAwait(false);
        }
    }
}
