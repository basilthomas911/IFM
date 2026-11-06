using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Query;
/// <summary>Reads safe host-published setup references without querying command state.</summary>
public static class GetDatabaseBackupSetup
{

    extension(GetDatabaseBackupSetupQuery query)
    {
        /// <summary>Returns configured host references or explicit unavailable metadata.</summary>
        public async ValueTask<ServiceResult<DatabaseBackupSetupReadModel>> ExecuteAsync(IDatabaseBackupOutputReader? outputReader, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            var metadata = outputReader is null
                ? new DatabaseBackupSetupReadModel()
                : await outputReader.ReadSetupAsync(query.Source, cancellationToken).ConfigureAwait(false);
            return new ServiceOk<DatabaseBackupSetupReadModel>(metadata);
        }
    }
}
