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
    /// <summary>Reads the requested Database Backup projection and sends its typed response.</summary>
    public static async ValueTask ExecuteAsync(this GetDatabaseProtectionSetsQuery query, ISystemAdminDbContext dbContext, IQueryActorContext<DatabaseBackupServiceQueryActor> context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(query);
        var result = await (dbContext.GetProtectionSetsAsync(query, cancellationToken)).ConfigureAwait(false);
        await (DatabaseBackupQueryReply.RequiredAsync(context, query, result)).ConfigureAwait(false);
    }
}
