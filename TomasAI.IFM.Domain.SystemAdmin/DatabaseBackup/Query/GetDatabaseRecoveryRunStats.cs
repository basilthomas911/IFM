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
        var result = await (dbContext.GetRecoveryRunStatsAsync(query, cancellationToken)).ConfigureAwait(false);
        await (DatabaseBackupQueryReply.OptionalAsync(context, query, result)).ConfigureAwait(false);
    }
}
