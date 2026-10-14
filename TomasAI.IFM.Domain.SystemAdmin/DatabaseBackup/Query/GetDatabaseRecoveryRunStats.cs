using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Model;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query;

/// <summary>Handles <see cref="GetDatabaseRecoveryRunStatsQuery"/>.</summary>
public static class GetDatabaseRecoveryRunStats
{
    /// <summary>Reads the requested Database Backup projection and sends its typed response.</summary>
    public static async ValueTask ExecuteAsync(this GetDatabaseRecoveryRunStatsQuery query, ISystemAdminDbContext dbContext, IQueryActorContext<DatabaseBackupQueryActor> context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(query);
        var rows = await dbContext.GetRecoveryRunStatisticsAsync(query.OperationId!.Value.Value, cancellationToken).ConfigureAwait(false);
        var result = BuildReadModel(query, rows);
        await (DatabaseBackupQueryReply.OptionalAsync(context, query, result)).ConfigureAwait(false);
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
