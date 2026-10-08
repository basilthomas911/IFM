using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query;
/// <summary>Reads safe host-published setup references without querying command state.</summary>
public static class GetDatabaseBackupSetup
{
    /// <summary>Replies with configured host references or explicit unavailable metadata.</summary>
    public static async ValueTask ExecuteAsync(this GetDatabaseBackupSetupQuery query, ISystemAdminDbContext dbContext, IQueryActorContext<DatabaseBackupQueryActor> context, CancellationToken cancellationToken)
    {
        var metadata = (context as IDatabaseBackupQueryContext)?.OutputReader is { } reader ? await reader.ReadSetupAsync(query.Source, cancellationToken) : new DatabaseBackupSetupReadModel();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<DatabaseBackupSetupReadModel>(metadata));
    }
}
