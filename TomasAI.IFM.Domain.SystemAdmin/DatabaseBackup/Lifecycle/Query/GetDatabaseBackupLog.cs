using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Lifecycle.Query;
/// <summary>Reads persisted phase history and authorized retained host output.</summary>
public static class GetDatabaseBackupLog
{

    extension(GetDatabaseBackupLogQuery query)
    {
        /// <summary>Checks the persisted operation/source before reading its bounded output page.</summary>
        public async ValueTask<ServiceResult<DatabaseBackupLogReadModel>> ExecuteAsync(ISystemAdminDbReadContext dbContext, IDatabaseBackupOutputReader? outputReader, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(query);
            var operation = await (dbContext.GetBackupOperationAsync(new() { OperationId = query.OperationId }, cancellationToken)).ConfigureAwait(false);
            if (operation is null || operation.Source != query.Source)
                return new ServiceFailed<DatabaseBackupLogReadModel>(404, "DatabaseBackup operation was not found for this source.");
            var phases = await (dbContext.GetBackupPhasesAsync(query, cancellationToken)).ConfigureAwait(false);
            var output = outputReader is null
                ? new DatabaseBackupLogReadModel()
                : await outputReader.ReadAsync(query.Source, operation.OperationId.Value, query.OutputOffset, cancellationToken).ConfigureAwait(false);
            return new ServiceOk<DatabaseBackupLogReadModel>(output with { Phases = phases });
        }
    }
}
