using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Model;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query;

/// <summary>Handles <see cref="GetDatabaseBackupPolicyQuery"/>.</summary>
public static class GetDatabaseBackupPolicy
{
    /// <summary>Reads the requested Database Backup projection and sends its typed response.</summary>
    public static async ValueTask ExecuteAsync(this GetDatabaseBackupPolicyQuery query, ISystemAdminDbContext dbContext, IQueryActorContext<DatabaseBackupQueryActor> context, CancellationToken cancellationToken)
    {
        var result = await dbContext.GetPolicyAsync(query, cancellationToken);
        await DatabaseBackupQueryReply.OptionalAsync(context, query, result);
    }
}
