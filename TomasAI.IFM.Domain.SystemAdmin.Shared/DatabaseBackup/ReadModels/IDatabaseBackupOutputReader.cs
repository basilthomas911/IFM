using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
/// <summary>Reads output only for a persisted, source-matched operation.</summary>
public interface IDatabaseBackupOutputReader
{
    /// <summary>Reads at most 64 KiB from the operation's output artifact.</summary>
    /// <summary>Reads the host-published allowlisted setup metadata.</summary>
    ValueTask<DatabaseBackupSetupReadModel> ReadSetupAsync(BackupSource source, CancellationToken cancellationToken) => ValueTask.FromResult(new DatabaseBackupSetupReadModel());
    ValueTask<DatabaseBackupLogReadModel> ReadAsync(BackupSource source, Guid operationId, long offset, CancellationToken cancellationToken);
}
