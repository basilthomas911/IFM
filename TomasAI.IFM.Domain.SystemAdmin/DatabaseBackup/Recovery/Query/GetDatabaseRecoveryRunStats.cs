using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Query;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Query;

/// <summary>Handles <see cref="GetDatabaseRecoveryRunStatsQuery"/>.</summary>
public static class GetDatabaseRecoveryRunStats
{

    extension(GetDatabaseRecoveryRunStatsQuery query)
    {
        /// <summary>Reads the requested Database Backup projection and returns its typed result.</summary>
        public async ValueTask<ServiceResult<DatabaseRecoveryRunStatsReadModel>> ExecuteAsync(ISystemAdminDbReadContext dbContext, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(query);
            var rows = await dbContext.GetRecoveryRunStatisticsAsync(query.OperationId!.Value.Value, cancellationToken).ConfigureAwait(false);
            var result = BuildReadModel(query, rows);
            return DatabaseBackupQueryResult.Optional(result);
        }
    }
    /// <summary>Builds run statistics and latest revision from persisted observations.</summary>
    internal static DatabaseRecoveryRunStatsReadModel? BuildReadModel(GetDatabaseRecoveryRunStatsQuery query, DatabaseBackupStatisticsObservation[] rows)
    {
        if (rows.Length == 0) return null;
        return new DatabaseRecoveryRunStatsReadModel
        {
            OperationId = query.OperationId.Value,
            Source = rows[0].Source,
            StatisticsRevision = rows.Max(static row => row.Revision),
            Statistics = [.. rows.Select(static row => row.Statistics)]
        };
    }
}
