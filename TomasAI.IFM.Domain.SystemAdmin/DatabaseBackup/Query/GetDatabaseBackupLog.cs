using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query;
/// <summary>Reads persisted phase history and authorized retained host output.</summary>
public static class GetDatabaseBackupLog
{
    /// <summary>Checks the persisted operation/source before reading its bounded output page.</summary>
    public static async ValueTask ExecuteAsync(this GetDatabaseBackupLogQuery query, ISystemAdminDbContext dbContext, IQueryActorContext<DatabaseBackupQueryActor> context, CancellationToken cancellationToken)
    {
        var operation = await dbContext.GetBackupOperationAsync(new() { OperationId = query.OperationId }, cancellationToken);
        if (operation is null || operation.Source != query.Source)
        {
            await context.ReplyAsync<DatabaseBackupLogReadModel>(query.Subject.ThreadId, query.Subject.Verb, new ServiceFailed<DatabaseBackupLogReadModel>(404, "DatabaseBackup operation was not found for this source."));
            return;
        }
        var phases = await dbContext.GetBackupPhasesAsync(query, cancellationToken);
        var output = (context as IDatabaseBackupQueryContext)?.OutputReader is { } reader
            ? await reader.ReadAsync(query.Source, operation.OperationId.Value, query.OutputOffset, cancellationToken) : new DatabaseBackupLogReadModel();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<DatabaseBackupLogReadModel>(output with { Phases = phases }));
    }
}
